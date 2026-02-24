Feature: Programmable Command Settings
	As a developer setting up AdaptiveRemote for a device
	I want IR commands to be loaded from a settings file
	So that only configured commands are enabled and the correct payloads are sent

Scenario: Programmed commands are enabled when loaded from settings file
	Given the application is in the Ready phase
	Then I should see the 'Power' button is enabled

Scenario: Unprogrammed commands are disabled when not in settings file
	Given the application is in the Ready phase
	Then I should see the 'Mute' button is disabled
	And I should see the 'PowerOn' button is disabled
	And I should see the 'PowerOff' button is disabled

Scenario: Broadlink sends payload from settings file for Power command
	Given the application is in the Ready phase
	When I click on the 'Power' button
	Then I should see the Broadlink device recorded at least one inbound packet
	And the recorded Broadlink packet's raw payload should match the configured payload for 'Power'
	And no Broadlink packets should be marked as malformed
	And I should not see any error messages in the logs
