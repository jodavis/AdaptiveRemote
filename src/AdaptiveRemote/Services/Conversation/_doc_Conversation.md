# Conversation Subsystem Architecture & Design

## Overview
The Conversation subsystem manages all speech-related functionality: listening to user instructions, mapping them to commands, invoking those commands, and providing spoken feedback. It is not responsible for executing commands directly, but invokes handlers provided by other subsystems. UI updates are handled via a ViewModel, not directly by the subsystem.

## Responsibilities & Boundaries
- **Speech recognition:** Listens for user instructions and interprets them using the current recognition engine.
- **Speech synthesis:** Provides spoken feedback about actions taken.
- **Conversation state:** Tracks the state of a conversation, allowing context to affect how instructions are interpreted.
- **Command invocation:** Maps recognized instructions to commands and invokes their handlers.
- **Delegation:** Actual command execution and UI rendering are handled by other subsystems.

## Key Design Decisions
- **Component separation:**
  - `SpeechRecognition` (listening), `SpeechSynthesis` (speaking), and `ConversationStateMachine` (state management) are independent components, coordinated by `ConversationController`.
  - `ListeningController` prevents the system from hearing itself speak.
- **Speech engine:** Currently uses System.Speech; future plans include custom ML models for improved accuracy and tiered fallback to general models/LLMs.
- **Sample recording:** Can record interpreted speech samples for diagnosis and future training.

## Testability & Extensibility
- All components are designed for unit testability; speech interfaces are abstract and mockable.
- UI updates are performed via a testable ViewModel.
- Conversation logic is implemented in `ConversationStateExtensions` for fluent, testable state transformations.
- Speech components can be swapped by implementing the same abstract interfaces.
- No external extensibility is expected.

## Accessibility & Future Plans
- Accessibility is a core goal; future improvements will focus on speech accuracy and context-aware command mapping.
- Plans for custom ML models and tiered recognition systems to improve reliability.

## Updating This Document
Update this document only when the overall design or boundaries of the Conversation subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
