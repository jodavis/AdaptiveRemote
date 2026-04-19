Feature: Stub layout file loading

  Scenario: App loads layout from stub JSON file
    Given the application is not running
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Info' button is enabled
    And I should not see any error messages in the logs
