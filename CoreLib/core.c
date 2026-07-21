#include "core.h"

extern APP_GLOBAL_CONFIG app_global;

#define MOUNTMGR_DEVICE_NAME L"\\Device\\MountPointManager"
#define MOUNTMGRCONTROLTYPE 0x0000006D
#define IOCTL_MOUNTMGR_QUERY_POINTS CTL_CODE(MOUNTMGRCONTROLTYPE, 2, METHOD_BUFFERED, FILE_ANY_ACCESS)

typedef struct _MOUNTMGR_MOUNT_POINT
{
	ULONG SymbolicLinkNameOffset;
	USHORT SymbolicLinkNameLength;
} MOUNTMGR_MOUNT_POINT, *PMOUNTMGR_MOUNT_POINT;

typedef struct _MOUNTMGR_MOUNT_POINTS
{
	ULONG Size;
	ULONG NumberOfMountPoints;
	MOUNTMGR_MOUNT_POINT MountPoints[1];
} MOUNTMGR_MOUNT_POINTS, *PMOUNTMGR_MOUNT_POINTS;

static NTSTATUS flush_volume_cache(void)
{
	PMOUNTMGR_MOUNT_POINTS mounts;
	PMOUNTMGR_MOUNT_POINT mnt;
	OBJECT_ATTRIBUTES oa = {0};
	IO_STATUS_BLOCK isb;
	UNICODE_STRING us;
	HANDLE hdev, hvol;
	NTSTATUS st;
	ULONG bufsz = 0x2000;

	RtlInitUnicodeString(&us, MOUNTMGR_DEVICE_NAME);
	InitializeObjectAttributes(&oa, &us, OBJ_CASE_INSENSITIVE, NULL, NULL);

	st = NtCreateFile(&hdev, FILE_READ_ATTRIBUTES | SYNCHRONIZE, &oa, &isb, NULL,
		FILE_ATTRIBUTE_NORMAL, FILE_SHARE_READ | FILE_SHARE_WRITE, FILE_OPEN,
		FILE_NON_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT, NULL, 0);
	if (!NT_SUCCESS(st)) return st;

	mounts = _r_mem_allocate(bufsz);
	if (!mounts) { st = STATUS_NO_MEMORY; goto close_dev; }

	st = NtDeviceIoControlFile(hdev, NULL, NULL, NULL, &isb,
		IOCTL_MOUNTMGR_QUERY_POINTS, NULL, 0, mounts, bufsz);
	if (!NT_SUCCESS(st)) goto free_mounts;

	for (ULONG i = 0; i < mounts->NumberOfMountPoints; i++)
	{
		mnt = &mounts->MountPoints[i];
		us.Length = mnt->SymbolicLinkNameLength;
		us.MaximumLength = mnt->SymbolicLinkNameLength + sizeof(UNICODE_NULL);
		us.Buffer = PTR_ADD_OFFSET(mounts, mnt->SymbolicLinkNameOffset);

		if (us.Length >= 96 && RtlEqualMemory(us.Buffer, L"\\??\\Volume{", 22))
		{
			InitializeObjectAttributes(&oa, &us, OBJ_CASE_INSENSITIVE, NULL, NULL);
			st = NtCreateFile(&hvol, FILE_WRITE_DATA | SYNCHRONIZE, &oa, &isb, NULL,
				FILE_ATTRIBUTE_NORMAL, FILE_SHARE_READ | FILE_SHARE_WRITE, FILE_OPEN,
				FILE_NON_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT, NULL, 0);
			if (NT_SUCCESS(st))
			{
				st = _r_fs_flushfile(hvol);
				NtClose(hvol);
			}
		}
	}

free_mounts:
	_r_mem_free(mounts);
close_dev:
	NtClose(hdev);
	return st;
}

ULONG core_get_limit_value(void)
{
	return _r_calc_clamp(_r_config_getulong(L"AutoreductValue", DEFAULT_AUTOREDUCT_VAL), 0, 100);
}

ULONG core_get_interval_value(void)
{
	return _r_calc_clamp(_r_config_getulong(L"AutoreductIntervalValue", DEFAULT_AUTOREDUCTINTERVAL_VAL), 1, 1440);
}

ULONG core_get_danger_value(void)
{
	return _r_calc_clamp(_r_config_getulong(L"TrayLevelDanger", DEFAULT_DANGER_LEVEL), 0, 100);
}

ULONG core_get_warning_value(void)
{
	return _r_calc_clamp(_r_config_getulong(L"TrayLevelWarning", DEFAULT_WARNING_LEVEL), 0, 100);
}


BOOLEAN core_is_elevated(void)
{
	return _r_sys_iselevated();
}

BOOLEAN core_should_autoclean(void)
{
	R_MEMORY_INFO info;
	LONG64 ts;

	if (!_r_config_getboolean(L"AutoreductEnable", FALSE))
		return FALSE;

	_r_sys_getmemoryinfo(&info);

	if (info.physical_memory.percent < core_get_limit_value())
		return FALSE;

	ts = _r_unixtime_now() - _r_config_getlong64(L"StatisticLastReduct", 0);
	if (ts < AUTOREDUCT_COOLDOWN)
		return FALSE;

	return TRUE;
}

BOOLEAN core_should_interval_clean(void)
{
	LONG64 ts;

	if (!_r_config_getboolean(L"AutoreductIntervalEnable", FALSE))
		return FALSE;

	ts = _r_unixtime_now() - _r_config_getlong64(L"StatisticLastReduct", 0);
	if (ts < (LONG64)core_get_interval_value() * 60)
		return FALSE;

	return TRUE;
}

void core_get_memory_info(
	ULONG64 *phys_total, ULONG64 *phys_used, ULONG64 *phys_free, double *phys_pct,
	ULONG64 *page_total, ULONG64 *page_used, ULONG64 *page_free, double *page_pct,
	ULONG64 *cache_total, ULONG64 *cache_used, ULONG64 *cache_free, double *cache_pct)
{
	R_MEMORY_INFO info;
	_r_sys_getmemoryinfo(&info);

	*phys_total = info.physical_memory.total_bytes;
	*phys_used  = info.physical_memory.used_bytes;
	*phys_free  = info.physical_memory.free_bytes;
	*phys_pct   = info.physical_memory.percent_f;

	*page_total = info.page_file.total_bytes;
	*page_used  = info.page_file.used_bytes;
	*page_free  = info.page_file.free_bytes;
	*page_pct   = info.page_file.percent_f;

	*cache_total = info.system_cache.total_bytes;
	*cache_used  = info.system_cache.used_bytes;
	*cache_free  = info.system_cache.free_bytes;
	*cache_pct   = info.system_cache.percent_f;
}

BOOLEAN core_clean_memory(ULONG source, ULONG mask, CLEANUP_RESULT *result)
{
	MEMORY_COMBINE_INFORMATION_EX combine_ex = {0};
	SYSTEM_FILECACHE_INFORMATION sfci = {0};
	SYSTEM_MEMORY_LIST_COMMAND cmd;
	R_MEMORY_INFO info;
	ULONG64 before, after;
	NTSTATUS st;

	if (mask == 0)
		mask = _r_config_getulong(L"ReductMask2", REDUCT_MASK_DEFAULT);

	if (source == SOURCE_AUTO)
	{
		if (!_r_config_getboolean(L"IsAllowStandbyListCleanup", FALSE))
			mask &= ~REDUCT_MASK_FREEZES;
	}

	// enable required privileges (critical for memory cleanup to work)
	ULONG privileges[] = {
		SE_PROF_SINGLE_PROCESS_PRIVILEGE,
		SE_INCREASE_QUOTA_PRIVILEGE,
	};
	_r_sys_setprocessprivilege(NtCurrentProcess(), privileges, RTL_NUMBER_OF(privileges), TRUE);

	before = _r_sys_getmemoryinfo(&info);
	before = info.physical_memory.used_bytes;

	if (mask & REDUCT_WORKINGSET) {
		cmd = MemoryEmptyWorkingSets;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"MemoryEmptyWorkingSets");
	}

	if (mask & REDUCT_SYSTEMFILECACHE) {
		sfci.MinimumWorkingSet = MAXSIZE_T;
		sfci.MaximumWorkingSet = MAXSIZE_T;
		st = NtSetSystemInformation(SystemFileCacheInformationEx, &sfci, sizeof(sfci));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"SystemFileCacheInformation");
	}

	if (mask & REDUCT_MODIFIEDFILECACHE)
		flush_volume_cache();

	if (mask & REDUCT_MODIFIEDLIST) {
		cmd = MemoryFlushModifiedList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"MemoryFlushModifiedList");
	}

	if (mask & REDUCT_STANDBYLIST) {
		cmd = MemoryPurgeStandbyList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"MemoryPurgeStandbyList");
	}

	if (mask & REDUCT_STANDBYPRIORITY0LIST) {
		cmd = MemoryPurgeLowPriorityStandbyList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"MemoryPurgeLowPriorityStandbyList");
	}

	if (_r_sys_isosversiongreaterorequal(WINDOWS_8_1) && (mask & REDUCT_REGISTRYCACHE)) {
		st = NtSetSystemInformation(SystemRegistryReconciliationInformation, NULL, 0);
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"SystemRegistryReconciliationInformation");
	}

	if (_r_sys_isosversiongreaterorequal(WINDOWS_10) && (mask & REDUCT_COMBINEMEMORYLISTS)) {
		st = NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, &combine_ex, sizeof(combine_ex));
		if (!NT_SUCCESS(st))
			_r_log(LOG_LEVEL_ERROR, NULL, L"NtSetSystemInformation", st, L"SystemCombinePhysicalMemoryInformation");
	}

	after = _r_sys_getmemoryinfo(&info);
	after = info.physical_memory.used_bytes;

	_r_config_setlong64(L"StatisticLastReduct", _r_unixtime_now());

	if (result)
	{
		result->bytes_before = before;
		result->bytes_after = after;
		result->bytes_freed = (after < before) ? (before - after) : 0;
		result->mask_used = mask;
		_r_format_bytesize64(result->formatted, 64, result->bytes_freed);
	}

	if (_r_config_getboolean(L"LogCleanResults", FALSE))
	{
		WCHAR buf[64];
		_r_format_bytesize64(buf, 64, result ? result->bytes_freed : 0);
		_r_log_v(LOG_LEVEL_INFO, NULL, source == SOURCE_AUTO ? L"Cleanup (Auto)" :
			source == SOURCE_MANUAL ? L"Cleanup (Manual)" :
			source == SOURCE_HOTKEY ? L"Cleanup (Hotkey)" :
			source == SOURCE_CMDLINE ? L"Cleanup (Command-line)" : L"Unknown", 0, buf);
	}

	return TRUE;
}

static LPCWSTR core_get_string_en(ULONG uid)
{
	switch (uid)
	{
		case 4:  return L"Settings";
		case 10: return L"About";
		case 17: return L"Clean memory";
		case 18: return L"Physical memory";
		case 19: return L"Pagefile";
		case 20: return L"System working set";
		case 21: return L"Usage";
		case 22: return L"Available";
		case 23: return L"Total available";
		case 24: return L"General";
		case 25: return L"Memory cleaning";
		case 26: return L"Appearance";
		case 31: return L"Memory regions to be cleaned";
		case 32: return L"Memory management";
		case 38: return L"Always on top";
		case 39: return L"Load on system startup";
		case 40: return L"Start minimized";
		case 41: return L"Confirm memory cleaning start";
		case 44: return L"Select language";
		case 45: return L"Working set";
		case 46: return L"System file cache";
		case 47: return L"Standby list (without priority)";
		case 48: return L"Standby list";
		case 49: return L"Modified page list";
		case 50: return L"Combine memory lists";
		case 51: return L"Clean when above: (%)";
		case 52: return L"Clean every: (min.)";
		case 71: return L"Show memory cleaning results";
		case 75: return L"Memory was released.";
		case 76: return L"Required administrator privileges.";
		case 35: return L"Color indication";
		case 64: return L"Warning level (%)";
		case 65: return L"Danger level (%)";
		case 91: return L"Theme";
		case 92: return L"System default";
		case 93: return L"Light";
		case 94: return L"Dark";
		default: return NULL;
	}
}

LPCWSTR core_get_string(ULONG uid)
{
	static WCHAR buf[256];

	// try routine library first
	if (app_global.locale.table && _r_obj_gethashtablesize(app_global.locale.table))
	{
		LPWSTR str = _r_locale_getstring(uid);
		if (str)
		{
			_r_str_copy(buf, (LONG)RTL_NUMBER_OF(buf), str);
			return buf;
		}
	}

	// read .lng file directly
	static WCHAR lng_path[MAX_PATH];
	static R_INITONCE lng_init = PR_INITONCE_INIT;
	if (_r_initonce_begin(&lng_init))
	{
		_r_str_printf(lng_path, RTL_NUMBER_OF(lng_path), L"%s\\language\\memreduct-winui.lng", _r_app_getdirectory()->buffer);
		_r_initonce_end(&lng_init);
	}

	WCHAR language[128] = {0};
	WCHAR section[128];
	WCHAR ini_path[MAX_PATH];
	_r_str_printf(ini_path, RTL_NUMBER_OF(ini_path), L"%s\\data\\memreduct-winui.ini", _r_app_getdirectory()->buffer);
	GetPrivateProfileStringW(L"memreduct", L"Language", L"", language, 127, ini_path);

	if (language[0])
	{
		wcscpy_s(section, 127, language);

		WCHAR key[16];
		_r_str_printf(key, RTL_NUMBER_OF(key), L"%03u", uid);
		GetPrivateProfileStringW(section, key, L"", buf, 255, lng_path);

		if (buf[0])
			return buf;
	}

	// no translation found — return English default
	return core_get_string_en(uid);
}

ULONG core_locale_count(void)
{
	// try routine library first
	if (app_global.locale.available_list)
	{
		ULONG_PTR count;
		_r_queuedlock_acquireshared(&app_global.locale.lock);
		count = _r_obj_getlistsize(app_global.locale.available_list);
		_r_queuedlock_releaseshared(&app_global.locale.lock);
		return (ULONG)count;
	}

	// fallback: count sections in language\\memreduct-winui.lng
	WCHAR buf[4096];
	WCHAR lng_path[MAX_PATH];
	_r_str_printf(lng_path, RTL_NUMBER_OF(lng_path), L"%s\\language\\memreduct-winui.lng", _r_app_getdirectory()->buffer);
	if (GetPrivateProfileSectionNamesW(buf, RTL_NUMBER_OF(buf), lng_path) == 0)
		return 0;

	ULONG_PTR count = 0;
	for (LPWSTR p = buf; *p; p += wcslen(p) + 1)
		count++;
	return (ULONG)count;
}

BOOLEAN core_locale_get_name(ULONG index, LPWSTR buf, ULONG buf_size)
{
	if (!buf || !buf_size) return FALSE;
	buf[0] = L'\0';

	// try routine library first
	if (app_global.locale.available_list)
	{
		PR_STRING name = NULL;
		_r_queuedlock_acquireshared(&app_global.locale.lock);
		if (index == 0) name = app_global.locale.resource_name;
		else name = _r_obj_getlistitem(app_global.locale.available_list, index - 1);
		if (name) _r_str_copy(buf, (LONG)buf_size, name->buffer);
		_r_queuedlock_releaseshared(&app_global.locale.lock);
		return name != NULL;
	}

	// fallback: read section names from language\\memreduct-winui.lng
	WCHAR sections[4096];
	WCHAR lng_path[MAX_PATH];
	_r_str_printf(lng_path, RTL_NUMBER_OF(lng_path), L"%s\\language\\memreduct-winui.lng", _r_app_getdirectory()->buffer);
	if (GetPrivateProfileSectionNamesW(sections, RTL_NUMBER_OF(sections), lng_path) == 0)
		return FALSE;

	ULONG_PTR i = 0;
	for (LPWSTR p = sections; *p; p += wcslen(p) + 1)
	{
		if (i == index) { _r_str_copy(buf, (LONG)buf_size, p); return TRUE; }
		i++;
	}
	return FALSE;
}

ULONG_PTR core_locale_get_current(void)
{
	PR_STRING current_name;
	PR_STRING locale_name;
	ULONG_PTR count, result = SIZE_MAX;

	if (app_global.locale.available_list)
	{
		_r_queuedlock_acquireshared(&app_global.locale.lock);

		current_name = app_global.locale.current_name;
		count = _r_obj_getlistsize(app_global.locale.available_list);

		if (current_name && _r_obj_isstringempty(current_name))
			result = 0;
		else if (current_name)
		{
			for (ULONG_PTR i = 0; i < count; i++)
			{
				locale_name = _r_obj_getlistitem(app_global.locale.available_list, i);
				if (locale_name && _r_str_isequal(&current_name->sr, &locale_name->sr, TRUE))
				{
					result = i + 1;
					break;
				}
			}
		}

		_r_queuedlock_releaseshared(&app_global.locale.lock);
		return result;
	}

	// fallback: read Language from INI, find matching section in .lng
	WCHAR language[128] = {0};
	WCHAR ini_path[MAX_PATH];
	_r_str_printf(ini_path, RTL_NUMBER_OF(ini_path), L"%s\\data\\memreduct-winui.ini", _r_app_getdirectory()->buffer);
	GetPrivateProfileStringW(L"memreduct", L"Language", L"", language, 127, ini_path);
	if (!language[0]) return 0; // System default

	WCHAR sections[4096];
	WCHAR lng_path[MAX_PATH];
	_r_str_printf(lng_path, RTL_NUMBER_OF(lng_path), L"%s\\language\\memreduct-winui.lng", _r_app_getdirectory()->buffer);
	if (GetPrivateProfileSectionNamesW(sections, RTL_NUMBER_OF(sections), lng_path) == 0)
		return 0;

	ULONG_PTR i = 1;
	for (LPWSTR p = sections; *p; p += wcslen(p) + 1)
	{
		if (_wcsicmp(p, language) == 0)
			return i;
		i++;
	}
	return 0;
}

BOOLEAN core_locale_set(ULONG_PTR index)
{
	PR_STRING locale_name;

	_r_queuedlock_acquireexclusive(&app_global.locale.lock);

	if (index == 0)
	{
		_r_obj_swapreference(&app_global.locale.current_name, app_global.locale.resource_name);
	}
	else if (app_global.locale.available_list)
	{
		locale_name = _r_obj_getlistitem(app_global.locale.available_list, index - 1);

		if (locale_name)
			_r_obj_swapreference(&app_global.locale.current_name, locale_name);
	}

	_r_queuedlock_releaseexclusive(&app_global.locale.lock);

	return TRUE;
}
