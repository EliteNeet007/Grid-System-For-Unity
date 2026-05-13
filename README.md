# Unity Grid System

A flexible and extensible 2D grid framework for Unity featuring support for square, hexagonal and equilateral triangle geometries.

This repository contains:
- A reusable geometry-agnostic grid system.
- Multiple interchangeable grid geometries.
- Neighbor query utilities.
- World/Grid conversion helpers.
- A territory control prototype demonstrating practical usage of the system.

# Features
## Multiple Grid Geometries
The framework currently supports:
- Square grids.
- Hexagonal grids.
- Equilateral triangle grids.

Each geometry implements a shared interface, allowing gameplay to remain largely geometry-independent.

## Geometry Abstraction
Grid logic is separated from geometry implementation.
This allows:
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

## World Space Utilities
The framework includes utilities for:
- Grid-to-world conversion.
- World-to-grid conversion.
- Bounds calculation.
- Coordinate clamping.
- Cell positioning.
- Geometry-aware spatial queries.

## Territory Control Exmaple Project
The repository also includes a territory control prototype built using the grid system.
This example demonstrates:
- Practical gameplay usage of the framework.
- Board generation.
- Cell ownership systems.
- Territory expansion mechanics.
- AI interactions.
- Visual cell representation.
The example project exists primarily as a demonstration of how the grid framework can support gameplay systems while remaining reusable and geometry-driven.

# Core Design Goals
## Reusability
The system is designed to support multiple game genres and gameplay styles without coupling gameplay logic to a specific geometry.

## Extensibility
New grid geometries can be added without modifying the core grid container.

## Clarity
The project prioritizes:
- Clear naming.
- Explicit geometry behavior.
- Readable API's.
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