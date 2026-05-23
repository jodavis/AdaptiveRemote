Feature: Learning Mode UI
	As a developer setting up AdaptiveRemote for a device
	I want to enter learning mode
	So that I can program IR commands

Scenario: Entering and exiting learning mode manages button states
	Given the application is in the Ready phase

	# Verify states before programming mode
	Then I should see the 'Power' button is enabled
	And I should see the 'TiVo' button is enabled
	And I should see the 'Mute' button is disabled

	# Verify states after entering programming mode
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'Power' button is programmed
	And I should see the 'Mute' button is enabled
	And I should see the 'Mute' button is not programmed
	And I should see the 'TiVo' button is disabled
	And I should see the 'TiVo' button is not programmed
	#   Verify color contrast in programming mode
	When I run the accessibility contrast checker
	Then I should not see any accessibility contrast violations
	
	# Verify states are restored after exiting programming mode
	When I click on the 'Learn' button
	Then I should see the 'Power' button is enabled
	And I should see the 'TiVo' button is enabled
	And I should see the 'Mute' button is disabled

Scenario: Program a programmable command
	Given the application is in the Ready phase
	Then I should see the 'Volume Down' button is disabled
	
	# Program the Volume Down command
	When I click on the 'Learn' button
	Then I should see the 'Volume Down' button is enabled
	And I should see the 'Volume Down' button is not programmed
	When I click on the 'Volume Down' button
	Then I should see a modal message containing
		"""
		<h1>Programming 'Down'</h1>
		<p>Point your remote at the Broadlink device and press Down.</p>
		"""
	And the Broadlink device should be in learning mode
	#   Verify the button active color is valid
	When I run the accessibility contrast checker
	Then I should not see any accessibility contrast violations
	When I send the IR signal for Volume Down to the Broadlink device
	Then I should see the 'Volume Down' button is programmed
	And I should not see a modal message
	When I click on the 'Learn' button
	Then I should see the 'Volume Down' button is enabled
	And I should not see any error messages in the logs

	# Verify that the programmed command works
	When I click on the 'Volume Down' button
	Then I should see the Broadlink device sent the IR signal for Volume Down
	And no Broadlink packets should be marked as malformed
	And I should not see any error messages in the logs

	# Verify that the programmed command persists after shutting down and restarting
	When I click on the 'Exit' button
	And I wait for the application to shut down
	And I start the application
	Then I should see the application in the Ready phase
	And I should see the 'Volume Down' button is enabled
	When I click on the 'Volume Down' button
	Then I should see the Broadlink device sent the IR signal for Volume Down
	And I should not see any error messages in the logs

Scenario: Programming a programmable command fails with a device error
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	And I should see a modal message containing
		"""
		<h1>Programming 'Mute'</h1>
		<p>Point your remote at the Broadlink device and press Mute.</p>
		"""
	When the Broadlink device simulates a device error
	Then I should see an error message in the logs:
		"""
		Error programming IRCommand 'Mute'
		"""
	And I should not see a modal message
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is disabled
	And I should not see any error messages in the logs

Scenario: Programming a programmable command is cancelled
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	When I click on the 'Mute' button
	Then the Broadlink device should be in learning mode
	And I should see a modal message containing
		"""
		<h1>Programming 'Mute'</h1>
		<p>Point your remote at the Broadlink device and press Mute.</p>
		"""
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is disabled
	And I should see the 'Power' button is enabled
	And I should not see any error messages in the logs

Scenario: Programming a command is preempted by clicking a different programmable button
	Given the application is in the Ready phase
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is enabled
	And I should see the 'Mute' button is not programmed
	And I should see the 'Power' button is enabled
	And I should see the 'Power' button is programmed

	# Start learning Mute
	When I click on the 'Mute' button
	Then I should see a modal message containing
		"""
		<h1>Programming 'Mute'</h1>
		<p>Point your remote at the Broadlink device and press Mute.</p>
		"""

	# Click Power while Mute's modal is showing — should cancel Mute and start Power
	When I click on the 'Power' button
	Then I should see a modal message containing
		"""
		<h1>Programming 'Power'</h1>
		<p>Point your remote at the Broadlink device and press Power.</p>
		"""

	# Complete Power's learning cycle
	When I send the IR signal for Power to the Broadlink device
	Then I should see the 'Power' button is programmed
	And I should not see a modal message

	# Verify Mute was not programmed
	When I click on the 'Learn' button
	Then I should see the 'Mute' button is not programmed
	And I should not see any error messages in the logs

