Feature: Recipe Browser

  Scenario: Empty recipe list shows a message
    Given I am on the recipe browser page
    Then I see the empty recipes message

  Scenario: Existing recipes appear in the list
    Given a recipe named "Tomato Soup" exists
    And I am on the recipe browser page
    Then I see "Tomato Soup" in the recipe list
