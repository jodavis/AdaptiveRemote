Feature: CompiledLayoutService Endpoints

Scenario: Get active compiled layout
    Given CompiledLayoutService is running
    And the client has a valid Authorization token
	When the client calls GET /layouts/compiled/active on the CompiledLayoutService endpoint
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a CompiledLayout
    And the CompiledLayout in the response body has a TiVo command named "Up"
    And the CompiledLayout in the response body has a TiVo command named "Select"
    And the CompiledLayout in the response body has an IR command named "Power"
    And the CompiledLayout in the response body has a Lifecycle command named "Learn"
    And the CompiledLayout in the response body has a Lifecycle command named "Exit"
    And I should see a message that contains "GET /layouts/compiled/active" in the CompiledLayoutService logs
    And I should not see any warning or error messages in the CompiledLayoutService logs
