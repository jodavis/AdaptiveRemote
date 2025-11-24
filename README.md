Adaptive Remote
===============

This project is a remote control application for TV and other AV equipment. It is
designed to enable people with limited or total loss of mobility to still control
what they watch on TV.

The application accepts voice commands for people who still have the power of speech. 
It also has a large-button UI that can be activated by normal touch/mouse actions as 
well as specialized eye-gaze hardware.

Commands are sent to the devices over IP where supported. They can also be sent as
infrared signals for older devices that don't connect to a network.


Getting Started
---------------
The project requires Windows OS and [.NET8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

The application can be run from two projects. `AdaptiveRemote` is the main GUI application.
`AdaptiveRemote.Console` is the same GUI application, but launched as a console app that 
logs output to the terminal.

To build and run the application, clone the repository and `dotnet run` from either the
`AdaptiveRemote` or `AdaptiveRemote.Console` project directories. You can also open the
solution in Visual Studio 2022 or later and run either project.

Unit tests are in the `AdaptiveRemote.Tests` project. You can run them using `dotnet test` 
from that directory or through Visual Studio's Test Explorer.

Design documentation
--------------------
Architecture and design notes are stored alongside implementations using `_doc_*.md` filenames so they surface at the top of each folder. See:
- `AdaptiveRemote/Services/Lifecycle/_doc_Lifecycle.md`: Lifecycle subsystem
- `AdaptiveRemote/Services/Commands/_doc_Commands.md`: Remote control command model subsystem
- `AdaptiveRemote/Services/Broadlink/_doc_Broadlink.md`: Broadlink RM4 mini device driver for handling `IRCommand`
