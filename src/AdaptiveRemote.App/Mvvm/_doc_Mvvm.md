# Mvvm Subsystem Architecture & Design

Summary: Describes MvvmProperty<T>, a strongly-typed property change notification system that serves as the MVVM foundation without WPF dependencies.

## Overview
The Mvvm subsystem provides foundational support for MVVM architecture in the UI. It enables strongly-typed property change notification for model classes, similar to WPF's `DependencyProperty`, but without WPF dependencies.

## Responsibilities & Boundaries
- **Property change notification:** Defines `MvvmProperty<T>` for strongly-typed properties that trigger `INotifyPropertyChanging` and `INotifyPropertyChanged` events.
- **MVVM foundation:** Used as a base for model classes to support UI binding and state updates.
- **No automatic property binding:** While originally designed to support property-to-property binding, this feature was not needed due to Blazor's `StateHasChanged` pattern.

## Key Design Decisions
- **Type safety:** Properties are strongly typed; values must match the declared type, unlike WPF `DependencyProperty`.
- **Thread affinity:** No UI thread requirement, but not thread safe; property changes and events occur on the calling thread.
- **Performance:** Property values are stored in a simple dictionary, which is lightweight and suitable for small property sets.

## Usage Patterns & Limitations
- **Defining properties:** Follow the pattern seen in other `MvvmObject`-derived classes in `Models`, e.g. [`Command`](../Models/Command.cs).
- **Settable vs. immutable:** Only settable properties should use `MvvmProperty<T>`. Immutable properties defined at creation time should be implemented as simple properties.

## Testability
- Designed for unit testability; property change events and state can be easily verified in tests.

## Updating This Document
Update this document only when the overall design or boundaries of the Mvvm subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
