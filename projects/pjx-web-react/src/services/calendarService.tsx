import { stringify } from 'querystring';
import UserSubmitUtility from '../utils/userSubmitUtility';
import { config } from '../utils/runtimeConfig';

export type EventDeleteProps = {
  eventId?: number
};

export type EventApiProps = {
    id?: number,
    title: string,
    start: string,
    end?: string
};

export type EventApiResult = {
  eventId: number,
  title: string,
  start: string,
  end?: string
};

const baseUrl = `${config.apiDotnetUrl}/api/calendar/event`;

export default class CalendarService extends UserSubmitUtility {
    accessToken?: string;

    readAllEvent = (postObj: any):Promise<any> => {
      return this.getAccessToken()
        .then(token => {
          return this.apiGet(token, `${baseUrl}/readAll`, stringify(postObj))
        }).catch(error => {
          return Promise.reject(error);
        });
    }

    eventCreate = (postObj: any):Promise<any> => {
      return this.getAccessToken()
        .then(token => {
          return this.apiPost(token, `${baseUrl}/create`, postObj)
        }).catch(error => {
          return Promise.reject(error);
        });
    }

    eventUpdate = (postObj: any):Promise<any> => {
      return this.getAccessToken()
        .then(token => {
          return this.apiPut(token, `${baseUrl}/update`, postObj)
        }).catch(error => {
          return Promise.reject(error);
        });
    }

    eventDelete = (postObj: EventDeleteProps):Promise<boolean> => {
      return this.getAccessToken()
        .then(token => {
          return this.apiDelete(token, `${baseUrl}/delete`, stringify(postObj))
        }).catch(error => {
          return Promise.reject(error);
        });
    }
}
