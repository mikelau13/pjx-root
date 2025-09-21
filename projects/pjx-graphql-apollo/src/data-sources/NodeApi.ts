import {
  transformCity,
  transformCities
} from '../transforms/transforms';
import PjxRESTDataSource from './PjxRESTDataSource';

export default class NodeAPI extends PjxRESTDataSource {
  constructor() {
    super();
    this.baseURL = process.env.NODE_API_ENDPOINT;
  }

  async getAllCities() {
    const results = await this.get('/cities');
    return transformCities(results);
  }

  async getCityById(id) {
    const result = await this.get(`/city/${id}`);

    if (result.length > 0) {
      return transformCity(result[0]);
    } else {
      return {};
    }
  }
}
