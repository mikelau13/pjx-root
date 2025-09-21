
import 'dotenv/config';
import { ApolloServer, gql, IResolvers } from 'apollo-server';
import { CACHE_TIMES } from './constants';
import logger from 'winston';
import NodeAPI from './data-sources/NodeApi';
import { catchResolverErrors } from './utils/resolverUtils';

process
  .on('unhandledRejection', (reason, p) => {
    logger.error('Unhandled Rejection at Promise', reason, p);
  })
  .on('uncaughtException', err => {
    logger.error('Uncaught Exception thrown', err);
    process.exit(1);
  });

// The GraphQL schema sample
const typeDefs = gql`
  # A simple schema Book to start
  type Book {
    title: String
    author: String
  }

  type City {
    id: String
    name: String
    city: String
  }

  type Query {
    books: [Book],
    cities: [City],
    city(id: String): City
  }
`;

const books = [
    {
      title: 'Harry Potter and the Chamber of Secrets',
      author: 'J.K. Rowling',
    },
    {
      title: 'Jurassic Park',
      author: 'Michael Crichton',
    },
];

const resolvers: IResolvers = {
  Query: {
    books: () => books,
    cities: catchResolverErrors(async (parent, args, { dataSources }) => {
      return dataSources.nodeApi.getAllCities();
    }),
    city: catchResolverErrors(async (parent, { id }, { dataSources }) => {
      return dataSources.nodeApi.getCityById(id);
    })      
  }
};

// The ApolloServer constructor
const server = new ApolloServer({
  typeDefs,
  resolvers,
  cacheControl: {
    defaultMaxAge: 1,
    stripFormattedExtensions: false
  },
  formatError: error => {
    logger.error('Error apollo-server', error);
    return error;
  },
  dataSources: () => {
    return {
      nodeApi: new NodeAPI()
    };
  }
});

// The `listen` method launches a web server.
server.listen().then(({ url }) => {
  console.log(`NODE_API_ENDPOINT = ${process.env.NODE_API_ENDPOINT}`);
  console.log(`🚀  Server ready at ${url}`);
});
