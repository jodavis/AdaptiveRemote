Feature: Broadlink Device Integration
	As a user
	I want the application to communicate with Broadlink IR devices
	So that I can control my TV and AV equipment using the adaptive remote

Scenario: Broadlink receives Power command
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	And I should not see any warning or error messages in the logs
	When I click on the 'Power' button
	Then I should see the Broadlink device recorded at least one inbound packet
	And the recorded Broadlink packet's raw payload should not be empty
	And no Broadlink packets should be marked as malformed
	When I click on the 'Exit' button
	And I wait for the application to shut down
	Then I should not see any warning or error messages in the logs
