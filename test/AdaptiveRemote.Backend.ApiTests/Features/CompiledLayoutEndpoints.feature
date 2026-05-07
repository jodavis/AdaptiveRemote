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
    And the CompiledLayoutService logs contain a request log entry for GET /layouts/compiled/active
    And the CompiledLayoutService logs contain no warnings or errors
