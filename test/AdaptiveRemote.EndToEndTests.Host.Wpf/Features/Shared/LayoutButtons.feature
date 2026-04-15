Feature: Layout button verification
	As a user
	I want all expected buttons from the layout to be present and accessible
	So that I can control my TV and AV equipment

Scenario: All expected buttons from layout are present
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	# DPAD group
	And I should see the 'Up' button is enabled
	And I should see the 'Down' button is enabled
	And I should see the 'Left' button is enabled
	And I should see the 'Right' button is enabled
	And I should see the 'Select' button is enabled
	And I should see the 'Back' button is enabled
	And I should see the 'Power' button is enabled
	# WELL group
	And I should see the 'TiVo' button is enabled
	And I should see the 'Netflix' button is enabled
	And I should see the 'Guide' button is enabled
	# PLAYBACK group
	And I should see the 'Play' button is enabled
	And I should see the 'Pause' button is enabled
	And I should see the 'Record' button is enabled
	And I should see the 'Skip' button is enabled
	And I should see the 'Replay' button is enabled
	# GUTTER group
	And I should see the 'Learn' button is enabled
	And I should see the 'Exit' button is enabled
	When I click on the 'Exit' button
	And I wait for the application to shut down
	Then I should not see any error messages in the logs
