Feature: Programming Mode UI
	As a developer setting up AdaptiveRemote for a device
	I want to enter programming mode
	So that I can program IR commands

Scenario: Entering and exiting programming mode manages button states
	Given the application is in the Ready phase
	Then I should see the 'Power' button is enabled
	When I click on the 'Learn' button
	Then I should see the 'Power' button is disabled
	And I should see the 'Mute' button is disabled
	And I should see the 'TiVo' button is disabled
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Mute' button is disabled
	And I should see the 'TiVo' button is enabled
