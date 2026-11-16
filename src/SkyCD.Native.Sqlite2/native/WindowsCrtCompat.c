/*
 * Copyright (c) 2026 SkyCD contributors
 * SPDX-License-Identifier: MIT
 *
 * Empty CRT section sentinels and a no-op DLL callback for this C library.
 * Providing these definitions avoids linking the two unmarked MinGW
 * default startup units (cinitexe and dllmain).
 */
#if defined(_WIN32)
#include <windows.h>

typedef int (*skycd_int_initializer)(void);
typedef void (*skycd_void_initializer)(void);

#define SKYCD_CRT_SECTION(name) __attribute__((section(name), used))

SKYCD_CRT_SECTION(".CRT$XIA") skycd_int_initializer __xi_a[] = {0};
SKYCD_CRT_SECTION(".CRT$XIZ") skycd_int_initializer __xi_z[] = {0};
SKYCD_CRT_SECTION(".CRT$XCA") skycd_void_initializer __xc_a[] = {0};
SKYCD_CRT_SECTION(".CRT$XCZ") skycd_void_initializer __xc_z[] = {0};
SKYCD_CRT_SECTION(".CRT$XPA") skycd_void_initializer __xp_a[] = {0};
SKYCD_CRT_SECTION(".CRT$XPZ") skycd_void_initializer __xp_z[] = {0};
SKYCD_CRT_SECTION(".CRT$XTA") skycd_void_initializer __xt_a[] = {0};
SKYCD_CRT_SECTION(".CRT$XTZ") skycd_void_initializer __xt_z[] = {0};

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved)
{
    (void)module;
    (void)reason;
    (void)reserved;
    return TRUE;
}
#endif
