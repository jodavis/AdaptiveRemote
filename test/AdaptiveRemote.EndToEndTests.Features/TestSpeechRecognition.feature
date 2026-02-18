Feature: Test Speech Recognition Service
	As a test developer
	I want to be able to control speech recognition programmatically
	So that I can test the conversation UI

Scenario: Test speech recognition engine can trigger wake word
	Given the application is running with test speech recognition
	When I say "Hey Remote"
	Then the application should enter listening mode

Scenario: Test speech recognition engine can trigger stop listening
	Given the application is running with test speech recognition
	And the application is in listening mode
	When I say "Thank you"
	Then the application should exit listening mode
