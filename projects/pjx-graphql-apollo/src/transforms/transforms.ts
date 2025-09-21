export const transformCity = (result: any = {}) => {
  return {
    id: result.id,
    name: result.name,
    city: result.city
  };
};

export const transformCities = (results: any = {}) => {
  if (!Array.isArray(results)) {
    return [];
  }

  return results.map(item => {
    return transformCity(item);
  });
};
