/* tslint:disable */
/* eslint-disable */
// @generated
// This file was automatically generated and should not be edited.

// ====================================================
// GraphQL query operation: AllCities
// ====================================================

export interface AllCities_cities {
  __typename: "City";
  id: string | null;
  name: string | null;
  city: string | null;
}

export interface AllCities {
  cities: (AllCities_cities | null)[] | null;
}
