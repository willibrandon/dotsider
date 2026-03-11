---
title: Strings
description: User strings, metadata strings, and raw binary string scanning.
---

![Strings tab](../../../assets/screenshots/strings.png)

The **Strings** tab (`4`) extracts text from three sources:

- **User strings** — string literals from the `#US` metadata heap (the strings your code uses directly)
- **Metadata strings** — type names, method names, namespace names from the `#Strings` heap
- **Raw scan** — binary string extraction from the entire file, similar to the Unix `strings` command

## Minimum length

Use the `--min-len` / `-n` flag to control the minimum length for the raw binary scan:

```
dotsider MyApp.dll -n 8
```

The default minimum is 4 characters. Increase it to reduce noise in large assemblies.
