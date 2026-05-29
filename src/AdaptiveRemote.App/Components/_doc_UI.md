# UI Subsystem Architecture & Design

Summary: Describes Blazor-based UI rendering, eye-gaze accessibility (large buttons, high-contrast), MVVM binding, and component organization.

## Overview
The UI subsystem is responsible for rendering the application interface, handling user input (touch, mouse, eye-gaze), and displaying application state and progress. It is built with WPF (for hosting) and Blazor (for the main UI), with static assets managed in `wwwroot`.

## Responsibilities & Boundaries
- **Rendering:** Displays command buttons and application state to the user.
- **Input handling:** Invokes command handlers on button clicks/taps; supports eye-gaze hardware and large-button UI for accessibility.
- **Progress display:** Shows startup/shutdown progress and conversation state during speech recognition sessions.
- **Delegation:** Command execution, speech recognition, and configuration are handled by other subsystems.

## Key Design Decisions
- **Accessibility:**
  - Blazor is used for the main UI to support eye-gaze hardware.
  - Buttons are made as large as possible for easier targeting.
  - High-contrast color scheme (dark background, light text, yellow highlights) is used..
- **MVVM pattern:**
  - All UI logic is in controllers that update ViewModels (including [`Command`](../Models/Command.cs) objects).
  - UI layers are thin, only displaying ViewModel state, maximizing testability.
- **Component organization:**
  - Blazor components are organized by need; `RemoteLayout` recursively creates button groups for CSS structure.
  - Static assets (index.html, LESS/CSS) are organized by purpose (layout, theme, button, conversation, etc.).
  - WPF hosts BlazorWebView and displays early startup messages.
- **Performance:**
  - Application is designed to show any UI as quickly as possible, so startup delays are communicated to the user immediately.

## Testability
- UI layers are thin and bind only to ViewModels, enabling thorough unit testing without actual UI.

## Accessibility Considerations
- Eye-gaze and speech recognition are prioritized over keyboard/mouse accessibility.
- Future plans include automated contrast testing for color accessibility.

## Updating This Document
Update this document only when the overall design or boundaries of the UI subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
