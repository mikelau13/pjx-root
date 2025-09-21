import { gql } from 'apollo-boost';

export const GET_CITIES = gql`
  query AllCities {
    cities{
      id
      name
      city
    }
  }
`;
