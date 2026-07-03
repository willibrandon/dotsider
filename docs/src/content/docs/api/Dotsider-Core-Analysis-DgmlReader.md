---
title: "DgmlReader"
description: "Reads an ILC dependency-graph DGML file, emitted when publishing a Native AOT project with IlcGenerateDgmlFile. The format is a DirectedGraph document of nodes (id and label) and links (source depends on target, with a reason). Node labels equal the node names an mstat size report stores (MstatReader), which is how sizes join to dependency chains. Parsing streams the XML — the graphs run to hundreds of thousands of links — and never throws: unreadable files return null, and malformed nodes or links are skipped."
slug: api/dotsider.core.analysis.dgmlreader
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Reads an ILC dependency-graph DGML file, emitted when publishing a Native AOT project with
`IlcGenerateDgmlFile`. The format is a `DirectedGraph` document of nodes (id and
label) and links (source depends on target, with a reason). Node labels equal the node
names an mstat size report stores ([MstatReader](/api/dotsider.core.analysis.mstatreader/)), which is how sizes join to
dependency chains.

Parsing streams the XML — the graphs run to hundreds of thousands of links — and never
throws: unreadable files return null, and malformed nodes or links are skipped.

```csharp
public static class DgmlReader
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlReader**

## Methods

### Read(Stream)

Reads a dependency graph from a stream. The stream is left open.

**Parameters:**

- `stream` ([Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)): A readable stream positioned at the start of the document.

**Returns:** [DgmlGraph](/api/dotsider.core.analysis.models.dgmlgraph/)

The graph, or null when the content is not a DGML document.

```csharp
public static DgmlGraph? Read(Stream stream)
```

### Read(string)

Reads a dependency graph from a file.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the `.dgml.xml` file.

**Returns:** [DgmlGraph](/api/dotsider.core.analysis.models.dgmlgraph/)

The graph, or null when the file is missing or is not a DGML document.

```csharp
public static DgmlGraph? Read(string filePath)
```

