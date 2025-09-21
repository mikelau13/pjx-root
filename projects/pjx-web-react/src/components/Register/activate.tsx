import './activate.css';
import axios from 'axios';
import React, { FunctionComponent, useState } from "react";
import { withRouter, RouteComponentProps, Link } from 'react-router-dom';
import { IDENTITY_CONFIG } from '../../utils/authConst';
import { createStyles, Theme, withStyles, WithStyles } from '@material-ui/core/styles';
import Button from '@material-ui/core/Button';
import SaveIcon from '@material-ui/icons/Save';
import Title from '../Header/title';
import RegisterSteps from './steps';
import Loading from '../Common/loading';

const styles = (theme: Theme) =>
  createStyles({

  }
);


type ActivateProps = {
    showError: any
}


const Activate: FunctionComponent<ActivateProps & RouteComponentProps & WithStyles<typeof styles>> = props => {
    const [submissionState, setSubmission] = useState({isSubmitted: false, isValidated: false, step: 2});

    var qs = require('qs');
    var code = qs.parse(props.location.search, { ignoreQueryPrefix: true }).code;
    var username = qs.parse(props.location.search, { ignoreQueryPrefix: true }).username;

    const sendActivationToServer = (email:string, code:string) => {
        if(email.length && code.length) {           
            props.showError(null);
  
            axios.get(`${IDENTITY_CONFIG.activation_url}?activationCode=${code}&username=${email}`, { validateStatus: (status) => { return (status >= 200 && status < 300) || status === 400; } })
                .then(function (response) {
                    if(response.status === 200){
                        props.showError(null);
                        setSubmission(prevState => ({...prevState, isValidated: true, step: 3 }));
                    } else {
                      console.log(response);
                      Object.keys(response.data).forEach(key => {
                        props.showError(Reflect.get(response.data,key)[0]);
                      });
                      setSubmission(prevState => ({...prevState, isSubmitted: false }));
                    }
                })
                .catch(function (error) {
                    console.log(error);
                    props.showError('Unexpected Error, please try again later.');
                    setSubmission(prevState => ({...prevState, isSubmitted: false }));
                });    
        } else {
          props.showError('Invalid activation link.');
        }
    }

    const handleSubmit = () => {
      setSubmission(prevState => ({...prevState, isSubmitted: true }));
      sendActivationToServer(username, code);
    }

    return (
        <React.Fragment>
            <Title title={'Account Validation'} />
            <RegisterSteps step={submissionState.step} />
            {!submissionState.isSubmitted && 
              <div>
              <span>{`Hi ${username}, please click the button below to activate your account.`}</span>
              <div className="break" />
              <Button
                variant="contained"
                color="primary"
                size="large"
                startIcon={<SaveIcon />}
                onClick={handleSubmit}
              >
                {`Activate Account`}
              </Button>
              </div>
            }
            {submissionState.isSubmitted && 
                <div>
                    {!submissionState.isValidated && 
                      <Loading />
                    }
                    {submissionState.isValidated && 
                      <div>Successful!  You might now visit the <Link to="/dashboard">sign on page</Link> to login.</div>
                    }
                </div>
            }
        </React.Fragment>
    );
}

export default withStyles(styles)(withRouter(Activate));
