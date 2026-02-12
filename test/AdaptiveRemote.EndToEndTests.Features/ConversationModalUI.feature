Feature: Conversation Modal UI
	As a user
	I want to see modal messages when using the conversation system
	So that I know when the system is listening and can interact with it

Scenario: Conversation modal message displays when speech is recognized
	Given the application is running
	And the application is in the Ready state
	When I say "Hey Remote"
	Then I should see the text "I'm listening..." is visible
	When I say "Thank you"
	Then I should see the speaking message "You're welcome!" is visible
	And I should see the text "I'm listening..." is not visible
