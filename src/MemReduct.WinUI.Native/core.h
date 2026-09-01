#pragma once

#include "routine.h"

#ifdef CORELIB_EXPORTS
#define CORE_API __declspec(dllexport)
#else
#define CORE_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define REDUCT_WORKINGSET           0x01
#define REDUCT_SYSTEMFILECACHE      0x02
#define REDUCT_STANDBYPRIORITY0LIST 0x04
#define REDUCT_STANDBYLIST          0x08
#define REDUCT_MODIFIEDLIST         0x10
#define REDUCT_COMBINEMEMORYLISTS   0x20
#define REDUCT_REGISTRYCACHE        0x40
#define REDUCT_MODIFIEDFILECACHE    0x80

#define REDUCT_MASK_DEFAULT (REDUCT_WORKINGSET | REDUCT_SYSTEMFILECACHE | REDUCT_STANDBYPRIORITY0LIST | REDUCT_REGISTRYCACHE | REDUCT_COMBINEMEMORYLISTS | REDUCT_MODIFIEDFILECACHE)
#define REDUCT_MASK_FREEZES  (REDUCT_STANDBYLIST | REDUCT_MODIFIEDLIST)
#define REDUCT_MASK_ALL (REDUCT_WORKINGSET | REDUCT_SYSTEMFILECACHE | REDUCT_STANDBYPRIORITY0LIST | REDUCT_STANDBYLIST | REDUCT_MODIFIEDLIST | REDUCT_COMBINEMEMORYLISTS | REDUCT_REGISTRYCACHE | REDUCT_MODIFIEDFILECACHE)

typedef enum _CLEANUP_SOURCE { SOURCE_AUTO, SOURCE_MANUAL, SOURCE_HOTKEY, SOURCE_CMDLINE } CLEANUP_SOURCE;

typedef struct _CLEANUP_RESULT
{
	ULONG64 bytes_before;
	ULONG64 bytes_after;
	ULONG64 bytes_freed;
	ULONG mask_used;
	ULONG succeeded_mask;
	ULONG failed_mask;
	WCHAR formatted[64];
} CLEANUP_RESULT;

CORE_API BOOLEAN  core_is_elevated(void);
CORE_API void     core_get_memory_info(ULONG64 *pt, ULONG64 *pu, ULONG64 *pf, double *pp, ULONG64 *gt, ULONG64 *gu, ULONG64 *gf, double *gp, ULONG64 *ct, ULONG64 *cu, ULONG64 *cf, double *cp);
CORE_API BOOLEAN  core_clean_memory(ULONG source, ULONG mask, CLEANUP_RESULT *result);

CORE_API ULONG    core_locale_count(void);
CORE_API BOOLEAN  core_locale_get_name(ULONG index, LPWSTR buf, ULONG buf_size);
CORE_API ULONG_PTR core_locale_get_current(void);
CORE_API BOOLEAN  core_locale_set(ULONG_PTR index);
CORE_API LPCWSTR   core_get_string(ULONG uid);

#ifdef __cplusplus
}
#endif
