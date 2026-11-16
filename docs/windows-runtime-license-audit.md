# Windows runtime license audit

The Zig 0.15.2 Windows x64 link was captured with `SKYCD_NATIVE_LINK_AUDIT=1`. Its COFF link command was replayed with `zig lld-link /map:<path>` and the map's linked archive members were matched to source files in Zig's bundled MinGW tree. The DLL startup object maps to `crt/crtdll.c` (explicitly public domain).

The two default units lacking explicit public-domain declarations, `crt/cinitexe.c` and `misc/dllmain.c`, are not linked: the project's MIT `WindowsCrtCompat.c` supplies independent empty CRT section sentinels and the no-op DLL callback. The remaining linked MinGW archive members below all have explicit public-domain declarations. Their complete leading notices are reproduced in `native/licenses/MinGW-Public-Domain.txt`. The package does not adopt MinGW's umbrella ZPL declaration for these public-domain exceptions.

- `libmingw32:CRT_fp10.obj`
- `libmingw32:gccmain.obj`
- `libmingw32:mingw_helpers.obj`
- `libmingw32:natstart.obj`
- `libmingw32:pesect.obj`
- `libmingw32:pseudo-reloc.obj`
- `libmingw32:tlsmcrt.obj`
- `libmingw32:tlssup.obj`
- `libmingw32:tlsthrd.obj`
- `libmingw32:ucrt___local_stdio_printf_options.obj`
- `libmingw32:ucrt_amsg_exit.obj`
- `libmingw32:ucrt_fprintf.obj`
- `libmingw32:ucrt_sprintf.obj`
- `libmingw32:ucrt_vfprintf.obj`

`compiler_rt:compiler_rt_zcu.obj` is Zig's MIT compiler runtime; its license is shipped separately. Windows system/UCRT libraries are dynamically imported and not bundled. Re-audit these inputs whenever the compiler or native link configuration changes.
