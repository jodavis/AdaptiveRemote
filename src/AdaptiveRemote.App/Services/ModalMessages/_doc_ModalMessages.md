# Modal Message Service — Design & Architecture

## Overview
The modal message service provides a single, centered overlay message visible on top of the remote
control UI. It is used by both the conversation (speech synthesis) subsystem and the programming
(IR learning) subsystem to display status and instruction messages to the user.

## Responsibilities & Boundaries
- **Message display:** Maintains a `ModalMessageView` whose `CurrentMessage` property is bound by the
  UI layer (`ConversationUI.razor`).
- **FIFO queuing:** Ensures only one message is shown at a time. Additional messages are queued and
  shown in arrival order once the current message is dismissed.
- **Keep-alive support:** A caller can request that a message remains visible after its body
  completes (e.g., while waiting for a spoken confirmation). The message is replaced when the next
  call to `ShowMessageAsync` arrives.
- **Markdown content:** The service stores message strings verbatim; callers may use markdown
  syntax for formatting (e.g., `**bold**`, `# title`). Rendering is handled by the UI layer.

## Design Decisions
- **Channel-based queue:** `System.Threading.Channels.Channel<T>` provides lock-free, FIFO ordering
  with a single background reader, avoiding the need for explicit locking.
- **ViewModel pattern:** `ModalMessageView` (a `MvvmObject` subclass) owns the UI-facing state.
  The service updates it; the Blazor component subscribes to `PropertyChanged` to re-render.
- **`keepAlive` flag:** When `true`, the message stays after `body` completes. The next
  `ShowMessageAsync` call replaces it atomically as the channel reader transitions to the next
  request. This supports confirmation prompts that must remain visible while the user responds.

## Queuing Requirements
- The application should **avoid issuing multiple `ShowMessageAsync` calls concurrently** where
  possible, to prevent unexpected queuing delays. In practice the conversation subsystem is
  sequential; the programming subsystem should similarly be sequential.
- If a keepAlive message is no longer needed, the caller should issue a new `ShowMessageAsync`
  with the next intended message (or allow the next natural message to replace it).

## Updating This Document
Update when the queuing strategy, ViewModel pattern, or markdown rendering approach changes.
For implementation details, refer to the source files and inline XML doc comments.
