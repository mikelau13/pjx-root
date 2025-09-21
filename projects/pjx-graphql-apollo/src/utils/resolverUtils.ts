import { CACHE_TIMES } from '../constants';
import { IFieldResolver } from 'graphql-tools';

export const catchResolverErrors: any = (
  resolverFn: IFieldResolver<any, any, any>
) => async (obj, args, context, info) => {
  try {
    const result = await resolverFn(obj, args, context, info);
    return result;
  } catch (err) {
    info.cacheControl.setCacheHint({ maxAge: CACHE_TIMES.NO_CACHE });
    return err;
  }
};
