Feature: Search Recipes

  Scenario: Typing in the search field filters the list
    Given a recipe named "Lemon Tart" exists
    And a recipe named "Beef Stew" exists
    And I am on the recipe browser page
    When I search for "Lemon"
    Then I see "Lemon Tart" in the recipe list
    And I do not see "Beef Stew" in the recipe list

  Scenario: Clearing the search shows all recipes
    Given a recipe named "Lemon Tart" exists
    And a recipe named "Beef Stew" exists
    And I am on the recipe browser page
    When I search for "Lemon"
    And I clear the search
    Then I see "Beef Stew" in the recipe list
