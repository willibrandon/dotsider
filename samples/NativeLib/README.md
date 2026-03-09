# NativeLib

Library demonstrating native interop patterns. Used to test dotsider's handling of P/Invoke declarations, unsafe IL opcodes, and fixed-size buffers.

- P/Invoke: `kernel32!GetCurrentProcessId`, `libc!getpid`, `user32!MessageBox`
- Unsafe pointer arithmetic and stack allocation (`stackalloc`)
- `FixedBuffer` struct with 256-byte fixed array
- `AllowUnsafeBlocks` enabled
