import React from 'react';
import { GET_CITIES } from '../../graphql/queries';
import { useQuery } from '@apollo/react-hooks';
import { withApollo } from '../../apollo/initApollo';
import './cityList.css';
import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper } from '@material-ui/core';
import Loading from '../Common/loading';
import Title from '../Header/title';

/**
 * Display a list of city.
 */
const CityList = () => {
    const { loading, error, data } = useQuery(GET_CITIES);

    if (error) return <h1>Error</h1>;
    if (loading) return <Loading />;

    return (
        <TableContainer component={Paper}>
          <Title title='City List' />
          <Table className={'city-table'}>
            <TableHead>
              <TableRow>
                <TableCell>{'City Id'}</TableCell>
                <TableCell align="right">{'City Name'}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.cities.map((data: any) => (
                <TableRow key={data.id}>
                  <TableCell component="th" scope="row">
                    {data.id}
                  </TableCell>
                  <TableCell align="right">{data.name}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
    );
};

export default withApollo({ ssr: false })(CityList);
