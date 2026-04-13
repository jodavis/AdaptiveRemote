@ApiIntegrationTest
Feature: RawLayoutService Endpoints

Scenario: List raw layouts when user has no layouts
    Given RawLayoutService is running
    When a test client calls GET /layouts/raw
    Then the response is 200 OK
    And the body is an empty RawLayout array

Scenario: Create a new raw layout
    Given RawLayoutService is running
    When a test client calls POST /layouts/raw with a valid RawLayout body
    Then the response is 201 Created
    And the body contains the created RawLayout with a generated Id
    And the service logs contain a request log entry for POST /layouts/raw
    And the service logs contain no warnings or errors

Scenario: Get raw layout by ID
    Given RawLayoutService is running
    And a raw layout exists with name "Test Layout"
    When a test client calls GET /layouts/raw/{id} for the created layout
    Then the response is 200 OK
    And the body deserializes to the created RawLayout

Scenario: Update an existing raw layout
    Given RawLayoutService is running
    And a raw layout exists with name "Original Layout"
    When a test client calls PUT /layouts/raw/{id} with updated name "Updated Layout"
    Then the response is 200 OK
    And the returned layout has name "Updated Layout"
    And the layout version is incremented

Scenario: Delete a raw layout
    Given RawLayoutService is running
    And a raw layout exists with name "Layout to Delete"
    When a test client calls DELETE /layouts/raw/{id} for the created layout
    Then the response is 204 No Content
    And getting the layout by ID returns 404 Not Found

Scenario: Access raw layouts without authentication
    Given RawLayoutService is running
    When an unauthenticated client calls GET /layouts/raw
    Then the response is 401 Unauthorized

Scenario: Get non-existent layout by ID
    Given RawLayoutService is running
    When a test client calls GET /layouts/raw/{id} with a random GUID
    Then the response is 404 Not Found

Scenario: Update non-existent layout
    Given RawLayoutService is running
    When a test client calls PUT /layouts/raw/{id} with a random GUID and name "Updated"
    Then the response is 404 Not Found

Scenario: Delete non-existent layout
    Given RawLayoutService is running
    When a test client calls DELETE /layouts/raw/{id} with a random GUID
    Then the response is 404 Not Found

Scenario: Create layout with invalid data
    Given RawLayoutService is running
    When a test client calls POST /layouts/raw with an invalid RawLayout body
    Then the response is 400 Bad Request

Scenario: Create layout with missing required fields
    Given RawLayoutService is running
    When a test client calls POST /layouts/raw with a RawLayout missing required fields
    Then the response is 400 Bad Request
