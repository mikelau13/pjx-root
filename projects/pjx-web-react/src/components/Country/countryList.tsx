import React, { FunctionComponent, useState, useEffect } from "react";
import { withRouter, RouteComponentProps } from 'react-router-dom';
import { createStyles, Theme, withStyles, WithStyles } from '@material-ui/core/styles';
import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper } from '@material-ui/core';
import AuthService from '../../services/authService';
import CountryService from '../../services/countryService';
import Title from '../Header/title';
import Loading from '../Common/loading';

const styles = (theme: Theme) =>
  createStyles({
    table: {
      minWidth: 400,
    }
  }
);

const CountryList: FunctionComponent<RouteComponentProps & WithStyles<typeof styles>> = (props) => {
    const { classes } = props;
    const [ countryList, setCountries ] = useState([]);
    const [ isLoading, setLoading ] = useState(true);
    const [ errorMsg, setError ] = useState('');

    useEffect(() => {
      fetchCountryList();
    }, []);    

    const fetchCountryList = () => {
      const authService = new AuthService();
      const countryService = new CountryService(authService);
      
      countryService.getCountryAll()
        .then(result => {
          setCountries(result);
          setLoading(false);
        })
        .catch(error => {
          console.log(`error: ${error}`);
          setLoading(false);
          setError('Unexpected Error');
        });
    }

    const renderCountryList = (countryList: any) => {
      return (
        <TableContainer component={Paper}>
          <Title title='Country List' />
          <Table className={classes.table}>
            <TableHead>
              <TableRow>
                <TableCell>{'Row Number'}</TableCell>
                <TableCell align="right">{'Country Name'}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {countryList.map((country: string, index: number) => (
                <TableRow key={country}>
                  <TableCell component="th" scope="row">
                    {index + 1}
                  </TableCell>
                  <TableCell align="right">{country}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      );
    }

    return (
      <React.Fragment>
        {isLoading &&
          <Loading />
        }
        {errorMsg !== '' &&
          <div>{errorMsg}</div>
        }
        {countryList.length > 0 &&
          renderCountryList(countryList)
        }
      </React.Fragment>
    );
}

export default withStyles(styles)(withRouter(CountryList));
