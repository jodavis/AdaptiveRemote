Feature: Learning Mode UI
	As a developer setting up AdaptiveRemote for a device
	I want to enter learning mode
	So that I can program IR commands

Scenario: Entering and exiting learning mode manages button states
	Given the application is in the Ready phase
	Then I should see the 'Power' button is enabled
	And I should see the 'Mute' button is disabled
	When I run the accessibility contrast checker
	Then I should not see any accessibility contrast violations
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Power' button is programmed
	And I should see the 'Mute' button is enabled
	And I should see the 'Mute' button is not programmed
	And I should see the 'TiVo' button is disabled
	When I run the accessibility contrast checker
	Then I should not see any accessibility contrast violations
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Mute' button is disabled
	And I should see the 'TiVo' button is enabled

Scenario: Program a programmable command
	Given the application is in the Ready phase
	Then I should see the 'Volume Down' button is disabled
	When I click on the 'Learn' button
	Then I should see the 'Volume Down' button is enabled
	And I should see the 'Volume Down' button is not programmed
	When I click on the 'Volume Down' button
	Then I should see a modal message containing "# Programming 'Down'"
	And the Broadlink device should be in learning mode
	When I send the IR signal for Volume Down to the Broadlink device
	Then I should see the 'Volume Down' button is programmed
	And I should not see a modal message
	When I click on the 'Learn' button
	Then I should see the 'Volume Down' button is enabled
	When I click on the 'Volume Down' button
	Then I should see the Broadlink device sent the IR signal for Volume Down
	When I click on the 'Exit' button
	And I wait for the application to shut down
	When I start the application
	Then I should see the application in the Ready phase
	And I should see the 'Volume Down' button is enabled

Scenario: Programming a programmable command fails with a device error
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	And I should see a modal message containing "# Programming 'Mute'"
	When the Broadlink device simulates a device error
	Then I should not see a modal message
	And I should see an error message in the logs:
		"""
		Error programming IRCommand 'Mute'
		"""
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is disabled

Scenario: Programming a programmable command is cancelled
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is disabled
	And I should see the 'Power' button is enabled
