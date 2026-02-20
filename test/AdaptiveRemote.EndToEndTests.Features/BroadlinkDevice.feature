Feature: Broadlink Device Integration
	As a user
	I want the application to communicate with Broadlink IR devices
	So that I can control my TV and AV equipment using the adaptive remote

Scenario: Broadlink receives Power command
	Given the application is in the Ready phase
	Then I should see the 'Power' button is enabled
	When I click on the 'Power' button
	Then I should see the Broadlink device recorded at least one inbound packet
	And the recorded Broadlink packet's raw payload should not be empty
	And no Broadlink packets should be marked as malformed
	And I should not see any error messages in the logs
