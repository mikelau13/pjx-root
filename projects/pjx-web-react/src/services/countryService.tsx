import axios from 'axios';
import AuthService from './authService';

export default class CountryService {
    authService: AuthService;

    constructor(authService: AuthService) {
        this.authService = authService;
    }

    private _callApi = (token: string):Promise<any> => {
        const headers = {
          Accept: 'application/json',
          Authorization: 'Bearer ' + token
        };
    
        return axios.get(`${process.env.REACT_APP_API_DOTNET_URL}/api/country/getall`, { headers });
    }

    getCountryAll(): Promise<any> {
        return this.authService.getUser()
        .then((user:any) => {
          if (user) {
            if (user.access_token) {
              return this._callApi(user.access_token)
              .then(result => {
                return Promise.resolve(result.data);
              }).catch(error => {
                if (error.response.status === 401) {
                    return this.authService.renewToken()
                    .then(renewedUser => {
                        return this._callApi(renewedUser.access_token);
                    })
                    .catch(error => {
                        return Promise.reject(error);
                    });
                } else {
                    return Promise.reject(error);
                }
              });
            } else if (user) {
              return this.authService.renewToken().then(renewedUser => {
                return this._callApi(renewedUser.access_token);
              });
            } else {
              return Promise.reject(new Error('user is not logged in'));
            }
          } else {
            return Promise.reject(new Error('no renewedUser'));
          }
        })
        .catch(error => {
            return Promise.reject(error);
        });
    };
}
