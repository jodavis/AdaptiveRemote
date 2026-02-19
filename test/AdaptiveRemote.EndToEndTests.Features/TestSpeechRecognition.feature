Feature: Test Speech Recognition Service
	As a test developer
	I want to be able to control speech recognition programmatically
	So that I can test the conversation UI

@speech
Scenario: Test speech recognition engine can trigger wake word and stop listening
	Given the application is running with test speech recognition
	When I say "Hey Remote"
	Then the application should enter listening mode
	When I say "Thank you"
	Then the application should exit listening mode
