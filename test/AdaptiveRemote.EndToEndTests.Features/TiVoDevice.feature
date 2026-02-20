Feature: TiVo Device Integration
	As a user
	I want the application to communicate with TiVo devices
	So that I can control my TiVo using the adaptive remote

Scenario: TiVo receives Play command
	Given the application is in the Ready phase
	Then I should see the 'Play' button is enabled
	When I click on the 'Play' button
	Then I should see the TiVo receives a "PLAY" message
	And I should not see any error messages in the logs
