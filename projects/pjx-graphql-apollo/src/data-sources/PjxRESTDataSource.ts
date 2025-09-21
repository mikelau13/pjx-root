import { RESTDataSource, RequestOptions } from 'apollo-datasource-rest';
import logger from '../logger';

/*
 Intercepting fetches for logger
 */
export default class PjxRESTDataSource extends RESTDataSource {
  willSendRequest(request: RequestOptions) {
    try {
      logger.info(
        `${this.constructor.name} request: ${this.baseURL}${request.path}`
      );
    } catch (err) {
      logger.info('Error logging request!');
    }
  }
}
