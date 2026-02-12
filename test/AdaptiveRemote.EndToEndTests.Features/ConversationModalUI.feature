Feature: Conversation Modal UI
	As a user
	I want to see modal messages when using the conversation system
	So that I know when the system is listening and can interact with it

Scenario: Conversation modal message displays when listening mode is activated
	Given the application is not running
	When I start the application
	Then I should see the application in the Ready phase
	And I should not see any warning or error messages in the logs
	When I click on the text 'Say "Hey Remote" to get my attention'
	Then I should see the text "I'm listening..." is visible
	When I click on the text "I'm listening..."
	Then I should see the text "I'm listening..." is not visible
	And I should see the text 'Say "Hey Remote" to get my attention' is visible
	When I click on the 'Exit' button
	And I wait for the application to shut down
	Then I should not see any warning or error messages in the logs
