import './title.css';
import React, { FunctionComponent } from 'react';
import {capitalize} from '../../utils/stringUtility';

type TitleProps = {
    title: string | null;
};

const Title: FunctionComponent<TitleProps> = props => {
    return(
        <h1 className="h1">{capitalize(props.title)}</h1>
    )
}

export default Title;
