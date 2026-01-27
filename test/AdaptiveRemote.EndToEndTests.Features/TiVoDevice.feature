@tivo
Feature: TiVo Device Integration
	As a user
	I want the application to communicate with TiVo devices
	So that I can control my TiVo using the adaptive remote

Scenario: TiVo receives Play command
	Given there is a simulated TiVo device
	And the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	And I should not see any warning or error messages in the logs
	When I click on the 'Play' button
	Then I should see the TiVo receives a "PLAY" message
	When I click on the 'Exit' button
	And I wait for the application to shut down
	Then I should not see any warning or error messages in the logs
