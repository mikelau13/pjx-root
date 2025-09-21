import React, { useState, useEffect, FunctionComponent } from 'react';
import './errorModal.css';

interface ErrorModalProps {
    hideError: any
    errorMessage: any
}
  
const ErrorModal: FunctionComponent<ErrorModalProps> = props => {
    const [modalDisplay, toggleDisplay] = useState('none');

    const openModal = () => {
        toggleDisplay('block');     
    };

    const closeModal = () => {
        toggleDisplay('none'); 
        props.hideError(null);
    };

    useEffect(() => {
        if(props.errorMessage !== null) {
            openModal()
        } else {
            closeModal()
        }
    });
    
    return(
        <div 
            id="alertPopUp"
            style={{ display: modalDisplay }}
        >
            <div>
                <span className="errorMessage">{props.errorMessage}</span>
                <button type="button" aria-label="Close" onClick={() => closeModal()}>
                    <span aria-hidden="true">&times;</span>
                </button>
            </div>
            
        </div>
    )
} 

export default ErrorModal
