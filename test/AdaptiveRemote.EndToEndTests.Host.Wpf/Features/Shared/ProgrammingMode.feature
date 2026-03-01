Feature: Programming Mode UI
	As a developer setting up AdaptiveRemote for a device
	I want to enter programming mode
	So that I can program IR commands

Scenario: Entering and exiting programming mode manages button states
	Given the application is in the Ready phase
	Then I should see the 'Power' button is enabled
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Mute' button is enabled
	And I should see the 'TiVo' button is disabled
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Mute' button is disabled
	And I should see the 'TiVo' button is enabled

Scenario: Program a programmable command
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	And I should see the 'Mute' button is not programmed
	When I click on the 'Mute' button
	Then I should see a modal message containing "Programming 'Mute'"
	And the Broadlink device should be in learning mode
	When I send an IR signal to the Broadlink device
	Then I should see the 'Mute' button is programmed
	And I should not see a modal message
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I clear the Broadlink recorded packets
	And I click on the 'Mute' button
	Then I should see the Broadlink device recorded at least one inbound packet
	And the recorded Broadlink packet's raw payload should match the newly learned data
	And no Broadlink packets should be marked as malformed

Scenario: Programming a programmable command fails with a device error
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	When the Broadlink device simulates a device error
	Then I should not see a modal message
	And I should see the 'Mute' button is not programmed

Scenario: Programming a programmable command is cancelled
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is disabled
	And I should see the 'Power' button is enabled
