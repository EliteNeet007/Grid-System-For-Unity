# Unity Grid Framework

A modular and extensible 2D grid framework for Unity designed to support geometry-agnostic gameplay systems across square, hexagonal, and equilateral triangle layouts.

This repository contains:
- A reusable geometry-agnostic grid system.
- Multiple interchangeable grid geometries.
- Neighbor query utilities.
- World/Grid conversion helpers.
- A territory control prototype demonstrating practical usage of the system.

# Why This Project Exists
Many grid systems are tightly coupled to a single geometry type, making gameplay systems difficult to reuse across different board layouts.

This project was created to provide a modular approach where gameplay systems operate against a shared grid API while geometry implementations define spatial behavior independently.

# Contents

- [Features](#features)
    - [Multiple Grid Geometries](#multiple-grid-geometries)
    - [Geometry Abstraction](#geometry-abstraction)
    - [Neighbor Query Support](#neighbor-query-support)
    - [World Space Utilities](#world-space-utilities)
    - [Territory Control Example Project](#territory-control-example-project)

- [Architecture Overview](#architecture-overview)
    - [Grid2D](#grid2d)
    - [Grid Geometry](#grid-geometry)
    - [Neighbor Logic](#neighbor-logic)
    - [Example Game Layer](#example-game-layer)

- [Design Principles](#design-principles)
    - [Geometry Isolation](#geometry-isolation)
    - [Separation of Responsibilities](#separation-of-responsibilities)
    - [Extensibility First](#extensibility-first)
    - [Explicit Behavior](#explicit-behavior)
    - [Gameplay Framework Separation](#gameplay-framework-separation)

- [Quick Start](#quick-start---using-the-grid-framework)
- [Core Design Goals](#core-design-goals)
- [Technical Information](#technical-information)
- [License](#license)

# Features
## Multiple Grid Geometries
The framework supports:

- Square grid:

![Square Grid](Media/square_grid_image.png)

- Hexagonal grid:

![Hexagonal Grid](Media/hexagon_grid_image.png)

- Triangle grid:

![Triangle Grid](Media/triangle_grid_image.png)

Each geometry implements a shared interface exposed through a consistent API.

## Geometry Abstraction
Grid logic is separated from geometry implementation.
Benefits include:
- Reusing gameplay systems across different grid types.
- Swapping geometries without rewriting core systems.
- Creating new custom geometries by implementing the shared geometry interface.

## Neighbor Query Support
The system includes flexible neighbor querying logic.
Supported concepts include:
- Edge neighbors.
- Vertex neighbors.
- Combined neighbor queries.
- Geometry-specific neighbor rules.
Different geometries can define their own adjacency behavior while exposing a consistent API.

Neighbor visualization examples:
Yellow: current cell.
Green: edge neighbors.
Blue: vertex neighbors.

- Square example:
![Square Example](Media/square_grid_gif.gif)

- Hexagonal example:
![Hexagonal Example](Media/hexagon_grid_gif.gif)

- Triangle example:
![Triangle Example](Media/triangle_grid_gif.gif)

## World Space Utilities
Included utilities:
- Grid-to-world conversion.
- World-to-grid conversion.
- Bounds calculation.
- Coordinate clamping.
- Cell positioning.
- Geometry-aware spatial queries.

## Territory Control Example Project
The repository also includes a territory control prototype built using the grid system.
This example demonstrates:
- Practical gameplay usage of the framework.
- Board generation.
- Cell ownership systems.
- Territory expansion mechanics.
- AI interactions.
- Visual cell representation.
The example project exists as a demonstration of how the grid framework can support gameplay systems while remaining reusable and geometry-driven.

Hexagonal grid territory control showcase:
![Territory Control Showcase](Media/territory_control_showcase_gif.gif)

# Architecture Overview
The grid framework is built around a separation between the grid data container and the geometry rules that define how the grid behaves in world space.

## Grid2D

`Grid2D<T>` acts as the core data container.

It is responsible for:
- Storing grid objects.
- Validating grid coordinates.
- Accessing objects by grid position.
- Providing neighbor query methods.
- Converting between grid and world positions through the assigned geometry.
- Exposing utility methods such as bounds checks and coordinate clamping.

The grid itself does not define the shape, spacing, or adjacency rules of its cells. Those responsibilities are delegated to the active geometry implementation.

## Grid Geometry

Each supported grid shape is implemented as a separate geometry class.

Included geometry implementations:
- Square grid geometry.
- Hexagonal grid geometry.
- Equilateral triangle grid geometry.

Each geometry defines:
- How cell coordinates map to world positions.
- How world positions map back to grid coordinates.
- Which cells count as neighbors.
- How edge and vertex relationships behave.
- How the full grid footprint is calculated.

That Enables the same high-level gameplay code to work across multiple grid types.

## Neighbor Logic

Neighbor queries are exposed through the grid, but the actual neighbor rules are geometry-specific.

For example:
- Square grids can support both edge and vertex neighbors.
- Hex grids use edge-based adjacency.
- Triangle grids support edge and vertex neighbor behavior.

This keeps gameplay systems from needing to know the internal rules of each shape.

## Example Game Layer

The territory control prototype sits above the grid system as a usage example.

It uses the grid framework for:
- Board construction.
- Cell lookup.
- Neighbor-based expansion.
- Territory ownership.
- Visual cell placement.

The game layer depends on the grid API, but the grid system itself does not depend on the territory control example.

# Design Principles
## Geometry Isolation
The grid container is separated from geometry-specific behavior.

Advantages:
- Gameplay systems remain geometry-agnostic.
- New geometries may be introduced without modifying existing gameplay code.
- Individual geometry implementations can evolve independently.

The goal is to treat geometry as a pluggable rule set rather than a hardcoded assumption.

## Separation of Responsibilities
The project attempts to keep systems focused on a single responsibility.

Examples include:
- Grid data storage being handled separately from geometry logic.
- Geometry implementations defining spatial behavior without storing gameplay data.
- Gameplay systems operating on the grid API without depending on geometry internals.

This improves maintainability and reduces coupling between systems.

## Extensibility First
The framework is designed around future expansion.

New systems should be addable without requiring large rewrites to existing code.

Examples include:
- Additional grid geometries.
- New neighbor relationship rules.
- Pathfinding systems.
- Procedural generation tools.
- Visualization and debugging utilities.

## Explicit Behavior
The project prioritizes clarity and predictability over hidden or overly abstract behavior.

This includes:
- Geometry implementations.
- Clear neighbor query types.
- Readable coordinate conversion methods.
- Directly named APIs and utility functions.

The system is designed to remain understandable as complexity increases.

## Gameplay Framework Separation
The territory control prototype exists as a usage example, not as a dependency of the framework itself.

The grid system is designed to function independently from any specific gameplay implementation.

This allows the framework to support a wide range of game genres and gameplay styles without being tied to a single use case.

# Quick Start - Using The Grid Framework
Basic examples for creating and interacting with the grid framework.

## Creating a Grid
Minimal constructor:
```csharp
Grid2D<CellData> grid = new Grid2D<CellData>(
    width, // grid width in cells.
    height, // grid height in cells.
    originPosition, // grid origin position - bottom-left corner.
    geometry // active grid geometry implementation - Square / Hexagonal / Equilateral Triangle.
);
```

The constructor can optionally receive additional values for:
- Cell Size (float) // the width/height of a single cell.
- Cell Spacing (float) // the space between cells.
- Grid Layout Type 2D (enum) // the grid's layout orientation - Vertical (x, y) / Horizontal (x, z) / Vertical Depth (z, y).
- Show Debug (bool) // displays a debug view of the grid we constructed.

## Validation
```csharp
if (grid.IsValidGridPosition(position)){}
```
Takes in a position - (x, y) or Vector2Int or Vector3, and returns true if the position is within the grid's area.

## World Conversion
```csharp
Vector3 worldPosition = grid.GetCellCenterWorldPosition(cellCoords);
```
Takes in coordinates - (x, y) or Vector2Int or Vector3, and returns the world position of the corresponding cell.

```csharp
Vector2Int cellCoords = grid.GetVectorInts(position);
```
Takes in a Vector3 position and returns the coordinates of the corresponding cell.

## Querying Neighbors
```csharp
grid.FillNeighborsBuffer(cellPosition, neighborsBuffer);
```
Takes in a cell position - (x, y) or Vector2Int or Vector3, As well as a Vector2Int[] neighborsBuffer.
Returns a filled buffer, can include invalid neighbor positions or filter these out before returning the result.

# Core Design Goals
## Reusability
The system is designed to support multiple game genres and gameplay styles without coupling gameplay logic to a specific geometry.

## Extensibility
New grid geometries can be added without modifying the core grid container.

## Clarity
The project prioritizes:
- Clear naming.
- Explicit geometry behavior.
- Readable APIs.
- Separation of responsibilities.

## Gameplay Flexibility
The framework is intended to support a wide range of systems, including:
- Strategy games.
- Territory control.
- Puzzle games.
- Tactical combat.
- Simulation systems.
- Procedural generation.
- Spatial gameplay mechanics.
- Building & inventory management systems.

# Technical Information
This project was built using Unity version `6000.3.9f1` (2D URP template), and uses the `old input system`.
This project requires no other packages to function as intended.

# License
This project is licensed under the MIT license.
See the `LICENSE` file for details.