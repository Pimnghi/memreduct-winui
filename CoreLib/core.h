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

#define DEFAULT_AUTOREDUCT_VAL 90
#define DEFAULT_AUTOREDUCTINTERVAL_VAL 30
#define AUTOREDUCT_COOLDOWN 30
#define DEFAULT_DANGER_LEVEL 90
#define DEFAULT_WARNING_LEVEL 70

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

typedef enum _CLEANUP_SOURCE
{
	SOURCE_AUTO,
	SOURCE_MANUAL,
	SOURCE_HOTKEY,
	SOURCE_CMDLINE
} CLEANUP_SOURCE;

typedef struct _CLEANUP_RESULT
{
	ULONG64 bytes_before;
	ULONG64 bytes_after;
	ULONG64 bytes_freed;
	ULONG   mask_used;
	WCHAR   formatted[64];
} CLEANUP_RESULT;

CORE_API ULONG    core_get_limit_value(void);
CORE_API ULONG    core_get_interval_value(void);
CORE_API ULONG    core_get_danger_value(void);
CORE_API ULONG    core_get_warning_value(void);
CORE_API ULONG    core_get_config_mask(void);
CORE_API BOOLEAN  core_is_elevated(void);
CORE_API BOOLEAN  core_should_autoclean(void);
CORE_API BOOLEAN  core_should_interval_clean(void);

CORE_API void     core_get_memory_info(
	ULONG64 *phys_total, ULONG64 *phys_used, ULONG64 *phys_free, double *phys_pct,
	ULONG64 *page_total, ULONG64 *page_used, ULONG64 *page_free, double *page_pct,
	ULONG64 *cache_total, ULONG64 *cache_used, ULONG64 *cache_free, double *cache_pct);

CORE_API BOOLEAN  core_clean_memory(ULONG source, ULONG mask, CLEANUP_RESULT *result);

// config getters/setters
CORE_API BOOLEAN  core_get_bool(LPCWSTR key, BOOLEAN default_val);
CORE_API void     core_set_bool(LPCWSTR key, BOOLEAN value);
CORE_API ULONG    core_get_uint(LPCWSTR key, ULONG default_val);
CORE_API void     core_set_uint(LPCWSTR key, ULONG value);
CORE_API LONG     core_get_int(LPCWSTR key, LONG default_val);
CORE_API void     core_set_int(LPCWSTR key, LONG value);
CORE_API void     core_set_config_mask(ULONG mask);

#ifdef __cplusplus
}
#endif
