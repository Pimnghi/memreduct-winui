#include "core.h"

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
	NTSTATUS last_failure = STATUS_UNSUCCESSFUL;
	BOOLEAN flushed_any = FALSE;
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

				if (NT_SUCCESS(st))
					flushed_any = TRUE;
				else
					last_failure = st;
			}
			else
				last_failure = st;
		}
	}

	if (flushed_any)
		st = STATUS_SUCCESS;
	else if (NT_SUCCESS(st))
		st = last_failure;

free_mounts:
	_r_mem_free(mounts);
close_dev:
	NtClose(hdev);
	return st;
}

BOOLEAN core_is_elevated(void)
{
	return _r_sys_iselevated();
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
	ULONG succeeded_mask = 0;
	ULONG failed_mask = 0;
	NTSTATUS st;

	(void)source;

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
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_WORKINGSET;
		else
			failed_mask |= REDUCT_WORKINGSET;
	}

	if (mask & REDUCT_SYSTEMFILECACHE) {
		sfci.MinimumWorkingSet = MAXSIZE_T;
		sfci.MaximumWorkingSet = MAXSIZE_T;
		st = NtSetSystemInformation(SystemFileCacheInformationEx, &sfci, sizeof(sfci));
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_SYSTEMFILECACHE;
		else
			failed_mask |= REDUCT_SYSTEMFILECACHE;
	}

	if (mask & REDUCT_MODIFIEDFILECACHE) {
		st = flush_volume_cache();
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_MODIFIEDFILECACHE;
		else
			failed_mask |= REDUCT_MODIFIEDFILECACHE;
	}

	if (mask & REDUCT_MODIFIEDLIST) {
		cmd = MemoryFlushModifiedList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_MODIFIEDLIST;
		else
			failed_mask |= REDUCT_MODIFIEDLIST;
	}

	if (mask & REDUCT_STANDBYLIST) {
		cmd = MemoryPurgeStandbyList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_STANDBYLIST;
		else
			failed_mask |= REDUCT_STANDBYLIST;
	}

	if (mask & REDUCT_STANDBYPRIORITY0LIST) {
		cmd = MemoryPurgeLowPriorityStandbyList;
		st = NtSetSystemInformation(SystemMemoryListInformation, &cmd, sizeof(cmd));
		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_STANDBYPRIORITY0LIST;
		else
			failed_mask |= REDUCT_STANDBYPRIORITY0LIST;
	}

	if (mask & REDUCT_REGISTRYCACHE) {
		if (_r_sys_isosversiongreaterorequal(WINDOWS_8_1))
			st = NtSetSystemInformation(SystemRegistryReconciliationInformation, NULL, 0);
		else
			st = STATUS_NOT_SUPPORTED;

		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_REGISTRYCACHE;
		else
			failed_mask |= REDUCT_REGISTRYCACHE;
	}

	if (mask & REDUCT_COMBINEMEMORYLISTS) {
		if (_r_sys_isosversiongreaterorequal(WINDOWS_10))
			st = NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, &combine_ex, sizeof(combine_ex));
		else
			st = STATUS_NOT_SUPPORTED;

		if (NT_SUCCESS(st))
			succeeded_mask |= REDUCT_COMBINEMEMORYLISTS;
		else
			failed_mask |= REDUCT_COMBINEMEMORYLISTS;
	}

	after = _r_sys_getmemoryinfo(&info);
	after = info.physical_memory.used_bytes;

	if (result)
	{
		result->bytes_before = before;
		result->bytes_after = after;
		result->bytes_freed = (after < before) ? (before - after) : 0;
		result->mask_used = mask;
		result->succeeded_mask = succeeded_mask;
		result->failed_mask = failed_mask;
		_r_format_bytesize64(result->formatted, 64, result->bytes_freed);
	}

	return succeeded_mask != 0 && failed_mask == 0;
}

static LPCWSTR core_get_string_en(ULONG uid)
{
	switch (uid)
	{
		case 4:  return L"Settings";
		case 5:  return L"Exit";
		case 10: return L"About";
		case 12: return L"Show / Hide";
		case 13: return L"Disable";
		case 14: return L"Clean areas";
		case 15: return L"Clean when above";
		case 16: return L"Clean every";
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
		case 27: return L"Tray icon";
		case 31: return L"Memory regions to be cleaned";
		case 32: return L"Memory management";
		case 33: return L"Shortcut";
		case 38: return L"Always on top";
		case 39: return L"Load on system startup";
		case 40: return L"Start minimized";
		case 41: return L"Confirm memory cleaning start";
		case 44: return L"Select language";
		case 45: return L"Working set";
		case 46: return L"System file cache";
		case 47: return L"Standby list (without priority)";
		case 48: return L"Standby list*";
		case 49: return L"Modified page list*";
		case 50: return L"Combine memory lists (Windows 10+)";
		case 51: return L"Clean when usage reaches threshold";
		case 52: return L"Clean at specified intervals";
		case 84: return L"Enable notifications sound";
		case 85: return L"Advanced";
		case 89: return L"Allow Standby list and Modified page list cleanup during automatic cleaning";
		case 90: return L"Log cleaning results into a debug log";
		case 95: return L"Registry cache (Windows 8.1+)";
		case 96: return L"Modified file cache";
		case 71: return L"Show memory cleaning results";
		case 72: return L"Are you sure you want to clean the memory?";
		case 75: return L"Memory was released.";
		case 76: return L"Required administrator privileges.";
		case 35: return L"Color indication";
		case 37: return L"Notifications";
		case 64: return L"Warning level threshold";
		case 65: return L"Danger level threshold";
		case 66: return L"Single click";
		case 67: return L"Middle click";
		case 68: return L"Show / Hide";
		case 69: return L"Clean memory";
		case 70: return L"Open task manager";
		case 91: return L"Theme";
		case 92: return L"System default";
		case 93: return L"Light";
		case 94: return L"Dark";
		case 97: return L"Dashboard";
		case 98: return L"minutes";
		case 99: return L"Press a new key combination";
		case 100: return L"The shortcut must include Win, Ctrl, Alt, or Shift.";
		case 101: return L"Save";
		case 102: return L"Cancel";
		case 103: return L"This shortcut is already in use.";
		case 104: return L"Version";
		case 105: return L"A lightweight real-time memory management application that uses the Windows Native API to clear system caches. Compatible with Windows 10/11 x64 and ARM64.";
		case 106: return L"Author";
		case 107: return L"Copyright";
		case 108: return L"Upstream project";
		case 109: return L"Open project repository";
		case 110: return L"Please run as administrator.";
		case 111: return L"Restart as administrator";
		case 112: return L"Memory cleaning failed";
		case 113: return L"The cleanup request could not be started.";
		case 114: return L"Failed areas: %s";
		case 115: return L"No significant memory was released.";
		case 116: return L"Memory released: %s. Failed areas: %m.";
		case 117: return L"No memory cleanup areas are selected.";
		case 118: return L"Usage: memreduct-winui.exe [-clean|-clean:full|-autostart]";
		case 119: return L"Memory cleaning partially failed (failed mask: %s).";
		case 120: return L"Memory cleaning failed (failed mask: %s).";
		case 121: return L"Administrator privileges required";
		case 122: return L"Mem Reduct WinUI command-line tool";
		case 123: return L"Show this help information.";
		case 124: return L"Clean memory using the areas selected in the current configuration.";
		case 125: return L"Clean all memory areas.";
		case 126: return L"Show memory usage in tray icon";
		case 127: return L"On";
		case 128: return L"Off";
		default: return NULL;
	}
}

static VOID core_get_locale_path(LPWSTR buffer, ULONG buffer_size)
{
	_r_str_printf(
		buffer,
		buffer_size,
		L"%s\\language\\memreduct-winui.lng",
		_r_app_getdirectory()->buffer);
}

static VOID core_get_config_path(LPWSTR buffer, ULONG buffer_size)
{
	_r_str_printf(
		buffer,
		buffer_size,
		L"%s\\data\\memreduct-winui.ini",
		_r_app_getdirectory()->buffer);
}

typedef struct _CORE_LOCALE_MAP
{
	LPCWSTR locale_prefix;
	LPCWSTR section_name;
} CORE_LOCALE_MAP;

static BOOLEAN core_locale_has_prefix(LPCWSTR locale_name, LPCWSTR prefix)
{
	SIZE_T prefix_length = wcslen(prefix);

	return _wcsnicmp(locale_name, prefix, prefix_length) == 0 &&
		(locale_name[prefix_length] == L'\0' || locale_name[prefix_length] == L'-');
}

static BOOLEAN core_get_system_locale(LPWSTR buffer, ULONG buffer_size)
{
	static const CORE_LOCALE_MAP locale_map[] =
	{
		{L"zh-Hant", L"Chinese (Traditional)"},
		{L"zh-TW", L"Chinese (Traditional)"},
		{L"zh-HK", L"Chinese (Traditional)"},
		{L"zh-MO", L"Chinese (Traditional)"},
		{L"zh", L"Chinese (Simplified)"},
		{L"pt-BR", L"Portuguese (Brazil)"},
		{L"pt", L"Portuguese"},
		{L"sr-Latn", L"Serbian (Latin)"},
		{L"sr", L"Serbian (Cyrillic)"},
		{L"ar", L"Arabic"},
		{L"bg", L"Bulgarian"},
		{L"ca", L"Catalan"},
		{L"cs", L"Czech"},
		{L"nl", L"Dutch"},
		{L"fr", L"French"},
		{L"de", L"German"},
		{L"he", L"Hebrew"},
		{L"hu", L"Hungarian"},
		{L"id", L"Indonesian"},
		{L"it", L"Italian"},
		{L"ja", L"Japanese"},
		{L"kk", L"Kazakh"},
		{L"ko", L"Korean"},
		{L"fa", L"Persian"},
		{L"pl", L"Polish"},
		{L"ro", L"Romanian"},
		{L"ru", L"Russian"},
		{L"sk", L"Slovak"},
		{L"es", L"Spanish"},
		{L"sv", L"Swedish"},
		{L"tr", L"Turkish"},
		{L"uk", L"Ukrainian"},
		{L"vi", L"Vietnamese"},
		{L"en", L"English"},
	};
	WCHAR locale_name[LOCALE_NAME_MAX_LENGTH];

	if (GetLocaleInfoW(
		LOCALE_USER_DEFAULT,
		LOCALE_SNAME,
		locale_name,
		RTL_NUMBER_OF(locale_name)) <= 1)
	{
		return FALSE;
	}

	for (ULONG_PTR i = 0; i < RTL_NUMBER_OF(locale_map); i++)
	{
		if (core_locale_has_prefix(locale_name, locale_map[i].locale_prefix))
		{
			_r_str_copy(buffer, (LONG)buffer_size, locale_map[i].section_name);
			return TRUE;
		}
	}

	return FALSE;
}

static BOOLEAN core_get_active_locale(LPWSTR buffer, ULONG buffer_size)
{
	WCHAR ini_path[MAX_PATH];

	buffer[0] = L'\0';
	core_get_config_path(ini_path, RTL_NUMBER_OF(ini_path));

	GetPrivateProfileStringW(
		L"memreduct",
		L"Language",
		L"",
		buffer,
		buffer_size,
		ini_path);

	if (buffer[0])
		return TRUE;

	// An empty configuration value means that the Windows user locale is used.
	if (core_get_system_locale(buffer, buffer_size))
		return TRUE;

	buffer[0] = L'\0';
	return FALSE;
}

LPCWSTR core_get_string(ULONG uid)
{
	static WCHAR buf[256];
	WCHAR language[128] = {0};
	WCHAR lng_path[MAX_PATH];

	if (core_get_active_locale(language, RTL_NUMBER_OF(language)))
	{
		WCHAR key[16];

		core_get_locale_path(lng_path, RTL_NUMBER_OF(lng_path));
		_r_str_printf(key, RTL_NUMBER_OF(key), L"%03u", uid);
		GetPrivateProfileStringW(
			language,
			key,
			L"",
			buf,
			RTL_NUMBER_OF(buf),
			lng_path);

		if (buf[0])
			return buf;
	}

	// no translation found — return English default
	return core_get_string_en(uid);
}

ULONG core_locale_count(void)
{
	WCHAR buf[4096];
	WCHAR lng_path[MAX_PATH];

	core_get_locale_path(lng_path, RTL_NUMBER_OF(lng_path));
	if (GetPrivateProfileSectionNamesW(buf, RTL_NUMBER_OF(buf), lng_path) == 0)
		return 1; // Built-in English is always available.

	ULONG_PTR count = 0;
	for (LPWSTR p = buf; *p; p += wcslen(p) + 1)
		count++;

	return (ULONG)count + 1; // English resource plus translated sections
}

BOOLEAN core_locale_get_name(ULONG index, LPWSTR buf, ULONG buf_size)
{
	if (!buf || !buf_size) return FALSE;
	buf[0] = L'\0';

	if (index == 0)
	{
		_r_str_copy(buf, (LONG)buf_size, L"English");
		return TRUE;
	}

	WCHAR sections[4096];
	WCHAR lng_path[MAX_PATH];

	core_get_locale_path(lng_path, RTL_NUMBER_OF(lng_path));
	if (GetPrivateProfileSectionNamesW(sections, RTL_NUMBER_OF(sections), lng_path) == 0)
		return FALSE;

	ULONG_PTR i = 1;
	for (LPWSTR p = sections; *p; p += wcslen(p) + 1)
	{
		if (i == index) { _r_str_copy(buf, (LONG)buf_size, p); return TRUE; }
		i++;
	}
	return FALSE;
}

ULONG_PTR core_locale_get_current(void)
{
	WCHAR language[128] = {0};
	WCHAR ini_path[MAX_PATH];

	core_get_config_path(ini_path, RTL_NUMBER_OF(ini_path));
	GetPrivateProfileStringW(L"memreduct", L"Language", L"", language, 127, ini_path);
	if (!language[0])
		return SIZE_MAX; // System default
	if (_wcsicmp(language, L"English") == 0)
		return 0;

	WCHAR sections[4096];
	WCHAR lng_path[MAX_PATH];

	core_get_locale_path(lng_path, RTL_NUMBER_OF(lng_path));
	if (GetPrivateProfileSectionNamesW(sections, RTL_NUMBER_OF(sections), lng_path) == 0)
		return SIZE_MAX;

	ULONG_PTR i = 1;
	for (LPWSTR p = sections; *p; p += wcslen(p) + 1)
	{
		if (_wcsicmp(p, language) == 0)
			return i;
		i++;
	}
	return SIZE_MAX;
}

BOOLEAN core_locale_set(ULONG_PTR index)
{
	WCHAR language[128] = {0};
	WCHAR ini_path[MAX_PATH];

	if (index == SIZE_MAX)
	{
		language[0] = L'\0';
	}
	else if (index == 0)
	{
		_r_str_copy(language, RTL_NUMBER_OF(language), L"English");
	}
	else if (!core_locale_get_name((ULONG)index, language, RTL_NUMBER_OF(language)))
	{
		return FALSE;
	}

	core_get_config_path(ini_path, RTL_NUMBER_OF(ini_path));

	return WritePrivateProfileStringW(
		L"memreduct",
		L"Language",
		language,
		ini_path);
}
