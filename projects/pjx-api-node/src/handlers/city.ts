import { Next, Request, Response } from 'restify';
import DummyDataService from '../services/dummyDataService';
import { NotFoundError } from 'restify-errors';

export const getCity = (req: Request, res:Response, next: Next) => {
    const { name }: { name: string } = req.params;
    const searchResult = DummyDataService.getDummyDataByCity(name);

    if (searchResult) {
        res.send(searchResult);
        return next();
    } else {
        return next(new NotFoundError(`City Name ${name} Not Found`));
    }
}

export const getCityById = (req: Request, res:Response, next: Next) => {
    const { cityId }: { cityId: number } = req.params;
    const searchResult = DummyDataService.getDummyDataById(cityId);

    if (searchResult && searchResult.length > 0) {
        res.send(searchResult);
        return next();
    } else {
        return next(new NotFoundError(`CityId ${cityId} Not Found`));
    }
}

export const getAllCities = (req: Request, res:Response, next: Next) => {
    const searchResult = DummyDataService.getDummyDataAll();

    if (searchResult && searchResult.length > 0) {
        res.send(searchResult);
        return next();
    } else {
        return next(new NotFoundError('City Not Found'));
    }
}
