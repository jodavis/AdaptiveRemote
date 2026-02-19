Feature: Conversation Modal UI
	As a test developer
	I want to verify the conversation modal message UI displays correctly
	So that I can ensure the modal works properly for users

Scenario: Speech synthesis displays modal message box
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	When I say "Hey Remote"
	Then the application should enter listening mode
	When I say "Thank you"
	Then the application should exit listening mode
	And I should see a modal with class "conversation-speaking-message" containing "You're welcome"
