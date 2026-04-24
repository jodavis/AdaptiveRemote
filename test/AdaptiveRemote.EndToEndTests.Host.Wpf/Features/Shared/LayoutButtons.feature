Feature: Layout button verification
	As a user
	I want all expected buttons from the layout to be present and accessible
	So that I can control my TV and AV equipment

Scenario: Layout CSS rules are present
	Given the application is in the Ready phase
	Then the stylesheet selector '#ROOT' should define 'display' as 'grid'
	And the stylesheet selector '#ROOT' should define 'grid-template-rows' as '6fr 3fr 1fr'
	And the stylesheet selector '#ROOT' should define 'grid-template-columns' as '3fr 2fr'
	And the stylesheet selector '#ROOT' should define 'grid-gap' as '20px'

Scenario: All expected buttons from layout are present
	Given the application is in the Ready phase
	# DPAD group
	Then I should see the 'Up' button exists
	And I should see the 'Down' button exists
	And I should see the 'Left' button exists
	And I should see the 'Right' button exists
	And I should see the 'Select' button exists
	And I should see the 'Back' button exists
	And I should see the 'Power' button exists
	# WELL group
	And I should see the 'TiVo' button exists
	And I should see the 'Netflix' button exists
	And I should see the 'Info' button exists
	# PLAYBACK group
	And I should see the 'Play' button exists
	And I should see the 'Pause' button exists
	And I should see the 'Record' button exists
	And I should see the 'Skip' button exists
	And I should see the 'Replay' button exists
	# CHANNELANDVOLUME group
	And I should see the 'Channel Up' button exists
	And I should see the 'Channel Down' button exists
	And I should see the 'Mute' button exists
	And I should see the 'Volume Up' button exists
	And I should see the 'Volume Down' button exists
	# GUTTER group
	And I should see the 'Learn' button exists
	And I should see the 'Exit' button exists
