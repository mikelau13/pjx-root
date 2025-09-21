import axios from 'axios';
import AuthService from '../services/authService';


export default class UserSubmitUtility {
    authService: AuthService;

    constructor(authService: AuthService) {
        this.authService = authService;
    }

    protected apiPost = (token: string, url: string, postObj: any):Promise<any> => {
      const headers = {
        Accept: 'application/json',
        Authorization: 'Bearer ' + token
      };
  
      return axios.post(url, postObj, { headers })
        .then(result => {
          return Promise.resolve(result.data);
        }).catch(error => {
          if (error.response.status === 401) {
            return this.authService.renewToken()
              .then(renewedUser => {
                headers.Authorization = 'Bearer ' + renewedUser.access_token;
                return axios.post(url, postObj, { headers })
                .then(result => {
                  return Promise.resolve(result.data);
                }).catch(error => {
                  return Promise.reject(new Error('user is not logged in'));
                });
              })
            .catch(error => {
                return Promise.reject(error);
            });
          } else {
              return Promise.reject(error);
          }
        });
    }

    protected apiPut = (token: string, url: string, postObj: any):Promise<any> => {
        const headers = {
            Accept: 'application/json',
            Authorization: 'Bearer ' + token
        };

        return axios.put(url, postObj, { headers })
            .then(result => {
            return Promise.resolve(result.data);
            }).catch(error => {
            if (error.response.status === 401) {
                return this.authService.renewToken()
                .then(renewedUser => {
                    headers.Authorization = 'Bearer ' + renewedUser.access_token;
                    return axios.put(url, postObj, { headers })
                    .then(result => {
                    return Promise.resolve(result.data);
                    }).catch(error => {
                    return Promise.reject(new Error('user is not logged in'));
                    });
                })
                .catch(error => {
                    return Promise.reject(error);
                });
            } else {
                return Promise.reject(error);
            }
        });
    }
	
    protected apiGet = (token: string, url: string, query_string: string):Promise<any> => {
        const headers = {
          contentType: 'application/json',
          Authorization: 'Bearer ' + token
        };
    
        return axios.get(`${url}?${query_string}`, { headers })
          .then(result => {
            return Promise.resolve(result.data);
          }).catch(error => {
            if (error.response.status === 401) {
              return this.authService.renewToken()
                .then(renewedUser => {
                  headers.Authorization = 'Bearer ' + renewedUser.access_token;
                  return axios.get(`${url}?${query_string}`, { headers })
                  .then(result => {
                    return Promise.resolve(result.data);
                  }).catch(error => {
                    return Promise.reject(new Error('user is not logged in'));
                  });
                })
              .catch(error => {
                  return Promise.reject(error);
              });
            } else {
                return Promise.reject(error);
            }
          });
    }

    protected apiDelete = (token: string, url: string, query_string: string):Promise<any> => {
        const headers = {
          contentType: 'application/json',
          Authorization: 'Bearer ' + token
        };
    
        return axios.delete(`${url}?${query_string}`, { headers })
          .then(result => {
            return Promise.resolve(result.data);
          }).catch(error => {
            if (error.response.status === 401) {
              return this.authService.renewToken()
                .then(renewedUser => {
                  headers.Authorization = 'Bearer ' + renewedUser.access_token;
                  return axios.delete(`${url}?${query_string}`, { headers })
                  .then(result => {
                    return Promise.resolve(result.data);
                  }).catch(error => {
                    return Promise.reject(new Error('user is not logged in'));
                  });
                })
              .catch(error => {
                  return Promise.reject(error);
              });
            } else {
                return Promise.reject(error);
            }
          });
    }

    protected getAccessToken = ():Promise<string> => {
      return this.authService.getUser()
        .then((user:any) => {
          if (user) {
            if (user.access_token) {
              return Promise.resolve(user.access_token);
            } else if (user) {
              return this.authService.renewToken().then(renewedUser => {
                return Promise.resolve(renewedUser.access_token);
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
    }
}
