import React, { Component } from "react";
import { Link } from 'react-router-dom';

export default class Dashboard extends Component {
    render() {
      return (
        <div>
          <h1>Dashboard</h1>
          <ul>
            <li><Link to="/cities">See All Cities</Link></li>
            <li><Link to="/calendar">See My Calendar</Link></li>
            <li><Link to="/logout">Sign Out</Link></li>
        </ul>
        </div>
      );
    }
  }
