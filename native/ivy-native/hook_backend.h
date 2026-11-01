#pragma once
#ifdef IVY_NATIVE_TEST
// Host control-flow tests do not include the ARM-only CPU context declarations.
extern "C" {
enum shadowhook_mode_t { SHADOWHOOK_MODE_UNIQUE = 1 };
int shadowhook_init(shadowhook_mode_t, bool);
void *shadowhook_hook_func_addr(void *, void *, void **);
int shadowhook_unhook(void *);
int shadowhook_get_errno();
const char *shadowhook_to_errmsg(int);
}
#else
#include "shadowhook.h"
#endif
