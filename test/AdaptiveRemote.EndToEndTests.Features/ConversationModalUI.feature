Feature: Conversation Modal UI
	As a test developer
	I want to verify the conversation modal message UI displays correctly
	So that I can ensure the modal works properly for users

Scenario: Activate and deactivate listening mode via UI click
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	And I should see the text "to get my attention"
	When I click on the text "to get my attention"
	Then the application should enter listening mode
	And I should see the text "I'm listening..."
	When I click on the text "I'm listening..."
	Then the application should exit listening mode
	And I should not see the text "I'm listening..."
	And I should see the text "to get my attention"
