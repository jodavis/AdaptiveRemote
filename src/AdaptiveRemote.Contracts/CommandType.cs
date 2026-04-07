namespace AdaptiveRemote.Contracts;

// Identifies the runtime command type. The client uses this to instantiate the correct
// App runtime type (TiVoCommand, IRCommand, LifecycleCommand, ActionCommand).
// Type-specific execution parameters are resolved by the client from its own configuration:
//   TiVo   — CommandId = Name.ToUpperInvariant() (existing convention)
//   IR     — payload programmed via remote, stored in ProgrammaticSettings
//   Others — keyed by Name
public enum CommandType { Lifecycle, TiVo, IR, Action }
