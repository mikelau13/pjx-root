import React, { FunctionComponent } from 'react';
import { makeStyles } from '@material-ui/core/styles';
import Stepper from '@material-ui/core/Stepper';
import Step from '@material-ui/core/Step';
import StepLabel from '@material-ui/core/StepLabel';

const useStyles = makeStyles((theme) => ({
  root: {
    width: '100%',
    margin: '10px'
  }
}));

function getSteps() {
  return ['Fill in Registration form', 'Check Validation Email', 'Validate Account'];
}

export type CountryListProps = {
    step: number
}

const RegisterSteps: FunctionComponent<CountryListProps> = (props) => {
  const classes = useStyles();
  const steps = getSteps();
  const { step } = props;

  return (
    <div className={classes.root}>
      <Stepper activeStep={step}>
        {steps.map((label, index) => {
          const stepProps = { completed: false };
          const labelProps = {};

          if (index < step) {
            stepProps.completed = true;
          }

          return (
            <Step key={label} {...stepProps}>
              <StepLabel {...labelProps}>{label}</StepLabel>
            </Step>
          );
        })}
      </Stepper>      
    </div>
  );
}

export default RegisterSteps;
