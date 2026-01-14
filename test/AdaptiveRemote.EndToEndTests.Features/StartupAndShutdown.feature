Feature: Application startup and shutdown
	As a user
	I want the application to start up and shut down cleanly

Scenario: Application starts up and shuts down without errors
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	And I should not see any warning or error messages in the logs
	And I should see the 'Play' button is enabled
	And I should see the 'Pause' button is enabled
	And I should see the 'Exit' button is enabled
	When I click on the 'Exit' button
	And I wait for the application to shut down
	Then I should not see any warning or error messages in the logs
