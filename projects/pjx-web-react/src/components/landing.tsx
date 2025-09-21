import React, {Component} from "react";
import Title from './Header/title';

export default class Landing extends Component {
  render() {
    return (
      <div>
        <Title title='Home' />
        <div>
          Welcome!
        </div>
      </div>
    );
  }
}
