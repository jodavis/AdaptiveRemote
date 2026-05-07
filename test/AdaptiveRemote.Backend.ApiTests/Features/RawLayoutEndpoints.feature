@ApiIntegrationTest
Feature: RawLayoutService Endpoints

Scenario: List raw layouts when user has no layouts
    Given RawLayoutService is running
    And the client has a valid Authorization token
    When the client calls GET /layouts/raw on the RawLayoutService endpoint
    Then the response is 200 OK
    And the response body is "[]"

Scenario: Create a new raw layout
    Given RawLayoutService is running
    And the client has a valid Authorization token
    When the client calls POST /layouts/raw on the RawLayoutService endpoint with
        """
        {
            "userId": "test-user",
            "name": "New Test Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "glyph": "↑",
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 1
                }
            ],
            "version": 1,
            "createdAt": "2026-05-06T08:30:00Z",
            "updatedAt": "2026-05-06T08:30:00Z",
            "validationResult": null
        }
        """
	Then the response is 201 Created
    And the response body is valid JSON
    And the response body represents a RawLayout
    And the RawLayout in the response body has a valid Id property
    And the RawLayoutService logs contain a request log entry for POST /layouts/raw
    And the RawLayoutService logs contain no warnings or errors

Scenario: Get raw layout by ID
    Given RawLayoutService is running
    And the client has a valid Authorization token
    And RawLayoutService has a raw layout with the name "Test Layout"
    When the client calls GET /layouts/raw/{id} on the RawLayoutService endpoint
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a RawLayout
    And the RawLayout in the response body has "name"="Test Layout"
    And the RawLayoutService logs contain no warnings or errors

Scenario: Update an existing raw layout
    Given RawLayoutService is running
    And the client has a valid Authorization token
    And RawLayoutService has a raw layout with the name "Original Layout"
    When the client calls PUT /layouts/raw/{id} on the RawLayoutService endpoint with
        """
        {
            "userId": "test-user",
            "name": "Updated Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "glyph": "↑",
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 1
                }
            ],
            "version": 1,
            "createdAt": "2026-05-06T08:30:00Z",
            "updatedAt": "2026-05-06T08:30:00Z",
            "validationResult": null
        }
        """
    Then the response is 200 OK
    And the response body is valid JSON
    And the response body represents a RawLayout
    And the RawLayout in the response body has "name"="Updated Layout"
    And the RawLayoutService logs contain no warnings or errors
    
    # Get the updated layout
    When the client calls GET /layouts/raw/{id} on the RawLayoutService endpoint
    Then the response is 200 OK
	And the response body represents a RawLayout
    And the RawLayout in the response body has "name"="Updated Layout"
    And the RawLayoutService logs contain no warnings or errors

Scenario: Delete a raw layout
    Given RawLayoutService is running
    And the client has a valid Authorization token
    And RawLayoutService has a raw layout with the name "Layout to Delete"
    When the client calls DELETE /layouts/raw/{id} on the RawLayoutService endpoint
    Then the response is 204 No Content
    And the response body is ""

    # Verify the layout was deleted
    When the client calls GET /layouts/raw/{id} on the RawLayoutService endpoint
    Then the response is 404 Not Found

Scenario: Access raw layouts without authentication
    Given RawLayoutService is running
    And the client has no Authorization token
	When the client calls GET /layouts/raw on the RawLayoutService endpoint
    Then the response is 401 Unauthorized

Scenario: Get non-existent layout by ID
    Given RawLayoutService is running
	And the client has a valid Authorization token
	When the client calls GET /layouts/raw/{random} on the RawLayoutService endpoint
    Then the response is 404 Not Found

Scenario: Update non-existent layout
    Given RawLayoutService is running
    And the client has a valid Authorization token
	When the client calls PUT /layouts/raw/{random} on the RawLayoutService endpoint with
        """
        {
            "userId": "test-user",
            "name": "Non-existent Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "glyph": "↑",
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 1
                }
            ],
            "version": 1,
            "createdAt": "2026-05-06T08:30:00Z",
            "updatedAt": "2026-05-06T08:30:00Z",
            "validationResult": null
        }
        """
    Then the response is 404 Not Found

Scenario: Delete non-existent layout
    Given RawLayoutService is running
    And the client has a valid Authorization token
    When the client calls DELETE /layouts/raw/{random} on the RawLayoutService endpoint
    Then the response is 404 Not Found

Scenario: Create layout with invalid data
    Given RawLayoutService is running
    And the client has a valid Authorization token
    When the client calls POST /layouts/raw on the RawLayoutService endpoint with
        # Missing comma between "glyph" and "speakPhrase" fields
        """
        {
            "userId": "test-user",
            "name": "Updated Layout",
            "elements": [
                {
                    "$type": "command",
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "glyph": "↑" 
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 1
                }
            ],
            "version": 1,
            "createdAt": "2026-05-06T08:30:00Z",
            "updatedAt": "2026-05-06T08:30:00Z",
            "validationResult": null
        }
        """
    Then the response is 400 Bad Request
    And the response body contains "Expected either ',', '}', or ']'."

Scenario: Create layout with missing required fields
    Given RawLayoutService is running
    And the client has a valid Authorization token
    When the client calls POST /layouts/raw on the RawLayoutService endpoint with
        # Element missing "$type" field
        """
        {
            "userId": "test-user",
            "name": "Updated Layout",
            "elements": [
                {
                    "type": 1,
                    "name": "Up",
                    "label": "Up",
                    "glyph": "↑", 
                    "speakPhrase": "up",
                    "reverse": "Down",
                    "cssId": "up-btn",
                    "gridRow": 0,
                    "gridColumn": 1
                }
            ],
            "version": 1,
            "createdAt": "2026-05-06T08:30:00Z",
            "updatedAt": "2026-05-06T08:30:00Z",
            "validationResult": null
        }
        """
    # TODO: I think this should be 400 Bad Request, but .NET is the one throwing
    Then the response is 500 Internal Server Error
    And the response body contains "The JSON payload for polymorphic interface or abstract type 'AdaptiveRemote.Contracts.RawLayoutElementDto' must specify a type discriminator."
