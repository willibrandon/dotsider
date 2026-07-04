#!/usr/bin/env bash
# Regenerates / cross-checks the native-disassembler oracle.
#
# The per-family unit suites (Xarch*Tests, Arm64*/Arm64Sve*Tests) pin every mnemonic and operand
# against these oracles, so they ARE the checked-in golden corpus. This script documents the exact
# oracle invocations so a maintainer can reproduce or extend them deterministically.
#
# x86-64 oracle — GNU objdump, Intel syntax (matches our rendering):
#   printf '<bytes>' > blob.bin
#   objdump -D -b binary -m i386:x86-64 -M intel --adjust-vma=0x1000 blob.bin
#
# AArch64 oracle — the stock WSL objdump is x86-only; use Capstone (no sudo):
#   pip install --user --break-system-packages capstone
#   python3 - <<'PY'
#   import capstone, struct
#   code = b"".join(struct.pack("<I", w) for w in WORDS)
#   md = capstone.Cs(capstone.CS_ARCH_ARM64, capstone.CS_MODE_LITTLE_ENDIAN)
#   for i in md.disasm(code, 0x1000): print(i.mnemonic, i.op_str)
#   PY
#
# Documented normalizer (oracle → our canonical form), applied when comparing:
#   - lowercase; Intel size hints ("dword ptr"); absolute hex for branch/RIP targets
#   - immediates as 0x-hex (Capstone's "#4" -> "0x4"); strip the AArch64 "#" prefix
#   - objdump's fused pclmul/vpcmp names collapse to the generic form (pclmulqdq/vpcmpd, imm)
#
# Zero-fallback / no-desync over real AOT output is enforced by NativeDisasmAotFixtureTests over the
# published NativeAotConsole and HardwareIntrinsics samples.
set -euo pipefail
echo "This script documents the oracle commands; see the comments above and the Xarch*/Arm64* test suites."
