Feature: Conversation Modal UI
	As a test developer
	I want to verify the conversation modal message UI displays correctly
	So that I can ensure the modal works properly for users

Scenario: Speech synthesis displays modal message box
	Given the application is in the Ready phase
	When I say "Hey Remote"
	Then I should see a modal message containing "I'm listening..."
	And the application should have spoken "I'm listening..."
	And the application should enter listening mode
	When I run the accessibility contrast checker
	Then I should not see any accessibility contrast violations
	When I say "Thank you"
	Then I should see a modal message containing "You're welcome"
	And the application should have spoken "You're welcome"
	And the application should exit listening mode
