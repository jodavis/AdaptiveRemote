Feature: Cloud layout update
	The application loads the remote control layout from a cloud asset (compiled layout service).
	The layout is cached locally. On startup, the cache is checked first; if present
	the app becomes Ready immediately while a background refresh runs. If the server
	returns a different layout after a scope recycle, the UI updates. If the compiled
	layout service changes while the app is running, the layout reloads and an idle-deferred recycle occurs.

@cloud-layout
Scenario: App starts successfully from compiled layout service when cache is empty
	Given the application is not running
	And the local layout cache is empty
	And the compiled layout service serves the primary layout
	When I start the application
	Then I should see the application in the Ready phase
	And I should see a message in the logs:
		"""
		Downloading asset 'layout'
		"""
	And I should not see the Guide button
	And I should not see any error messages in the logs

@cloud-layout
Scenario: App starts successfully from local cache when cache is present
	Given the application is not running
	And the local layout cache contains the primary layout
	And the compiled layout service serves the primary layout
	When I start the application
	Then I should see the application in the Ready phase
	And I should see a message in the logs:
		"""
		Loaded asset 'layout' from cache
		"""
	And I should see a message in the logs:
		"""
		Asset 'layout' is up to date
		"""
	And I should not see the Guide button
	And I should not see any error messages in the logs

@cloud-layout
Scenario: App shows updated layout after background refresh detects server change
	Given the application is not running
	And the local layout cache contains the primary layout
	And the compiled layout service serves the updated layout
	And the idle cooldown is 2 seconds
	When I start the application
	Then I should see the application in the Ready phase
	And I should see a message in the logs:
		"""
		Loaded asset 'layout' from cache
		"""
	And I should see a message in the logs:
		"""
		Asset 'layout' updated from server; scheduling recycle
		"""
	And I should see the application recycle
	And I should see the "Guide" button exists
	And I should not see any error messages in the logs

@cloud-layout
Scenario: App shows no update when cached layout matches server layout
	Given the application is not running
	And the local layout cache contains the primary layout
	And the compiled layout service serves the primary layout
	When I start the application
	Then I should see the application in the Ready phase
	And I should see a message in the logs:
		"""
		Asset 'layout' is up to date
		"""
	And I should not see a message containing 'recycling' in the logs
	And I should not see the Guide button
	And I should not see any error messages in the logs

@cloud-layout
Scenario: Layout updates after compiled layout service changes while app is running
	Given the application is not running
	And the local layout cache contains the primary layout
	And the compiled layout service serves the primary layout
	When I start the application
	Then I should see the application in the Ready phase
	And I should not see the Guide button
	When the compiled layout service is updated to the updated layout
	Then I should see a message in the logs:
		"""
		Layout service reported a change; re-downloading asset 'layout'
		"""
	And I should see the application recycle
	And I should see the application in the Ready phase
	And I should see the "Guide" button exists
	And I should not see any error messages in the logs

@cloud-layout
Scenario: App enters fatal error state when cache is empty and compiled layout service is unavailable
	Given the application is not running
	And the local layout cache is empty
	And the compiled layout service is unavailable
	When I start the application
	Then I should see a fatal startup error message

@cloud-layout
Scenario: App continues with cached layout when background server download fails
	Given the application is not running
	And the local layout cache contains the primary layout
	And the compiled layout service is unavailable
	When I start the application
	Then I should see the application in the Ready phase
	And I should see a message in the logs:
		"""
		Loaded asset 'layout' from cache
		"""
	And I should see a message in the logs:
		"""
		Failed to download latest 'layout' from server; keeping cached version
		"""
	And I should not see the Guide button
	And I should not see any error messages in the logs

