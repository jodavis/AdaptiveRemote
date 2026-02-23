Feature: Conversation Modal UI
	As a test developer
	I want to verify the conversation modal message UI displays correctly
	So that I can ensure the modal works properly for users

Scenario: Speech synthesis displays modal message box
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	When I say "Hey Remote"
	Then I should see a modal message containing "I'm listening..."
	And the application should enter listening mode
	When I say "Thank you"
	Then I should see a modal message containing "You're welcome"
	And the application should exit listening mode
